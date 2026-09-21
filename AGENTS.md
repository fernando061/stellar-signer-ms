# Contexto para agentes: APP_STELLAR_SIGNER

## JWT interno compartido

Para el MVP, Remittances emite un JWT interno corto con la misma firma HMAC configurada en ambos
servicios. El Signer valida `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey`,
`client_id=remittances-ms` y el scope `stellar-signer.execute`. No se reenvía el token del usuario
final, no intervienen refresh tokens y ningún secreto se incorpora al repositorio.

Este archivo es el punto de entrada para integrar `APP_REMITTANCES_MS` con este servicio. Describe el contrato implementado en este repositorio; si el código cambia, actualiza este documento junto con el cambio. Lee también [README.md](README.md) para despliegue, configuración y migraciones.

## Responsabilidades y límite de confianza

- `APP_REMITTANCES_MS` es dueño de socios, remesas, montos de negocio, estados, autorización comercial y transmisión de transacciones a Stellar. Proporciona `partnerId`, `remittanceId`, `requestId`, `Idempotency-Key` y el contexto esperado.
- `APP_STELLAR_SIGNER` deriva una wallet pública por socio y firma exclusivamente transacciones Soroban Testnet que coincidan con ese contexto y sus políticas internas. No crea ni financia la cuenta en la red; tampoco construye, simula ni transmite la transacción.
- `STELLAR_ESCROW_CONTRACT` es el contrato que recibe las invocaciones. El Signer solo permite el `Stellar__ContractId` configurado y el issuer `Stellar__Issuer` configurado. Ambos servicios deben usar esos mismos valores y la misma Testnet.
- La clave privada, la mnemonic, la seed y el payload maestro cifrado permanecen bajo custodia del Signer. Ningún servicio consumidor debe recibirlos ni persistirlos.

## Contrato HTTP implementado

Base local de Docker Compose: `http://127.0.0.1:8080`. Rutas internas bajo `/api/internal/v1`. En producción, usa una URL interna con HTTPS. Todos los endpoints siguientes devuelven JSON con propiedades `camelCase` y requieren `Authorization: Bearer <JWT>`; el JWT debe tener issuer y audience configurados en el Signer, no estar expirado, tener `client_id=remittances-ms` y contener `stellar-signer.execute` en `scope` o `scp`. Para este MVP se valida la firma HMAC compartida mediante `Jwt__SigningKey`; no hay secreto compartido hardcodeado.

| Método y ruta | Solicitud | Respuesta satisfactoria |
| --- | --- | --- |
| `POST /api/internal/v1/wallets` | JSON `{ "partnerId": "<UUID>" }` | `200` con `partnerId`, `publicKey`, `derivationIndex`, `derivationPath`, `status` |
| `GET /api/internal/v1/wallets/{partnerId}` | UUID en ruta | `200` con el mismo objeto; `404` si no existe |
| `POST /api/internal/v1/signatures` | Header `Idempotency-Key` y JSON indicado abajo | `200` con `requestId`, `transactionHash`, `signedTransactionXdr`, `status`, `signedAt` |
| `GET /api/internal/v1/signatures/{requestId}` | UUID en ruta | `200` con `requestId`, `transactionHash` nullable, `status`, `signedAt` nullable y `failureCode` nullable; nunca devuelve XDR |

`POST /wallets` es seguro para repetir con el mismo `partnerId`: devuelve la wallet existente. Su `publicKey` es una dirección Stellar `G...`; `derivationPath` tiene forma `m/44'/148'/{index}'`. Los UUID de negocio llegan desde el llamador y no se regeneran. Las PK internas de PostgreSQL son independientes y se generan allí.

Ejemplo de solicitud de firma (los valores entre `<>` son marcadores, no datos reales):

```http
POST /api/internal/v1/signatures
Authorization: Bearer <JWT>
Idempotency-Key: <clave-estable-de-la-operacion>
X-Correlation-ID: <identificador-de-traza-opcional>
Content-Type: application/json

{
  "requestId": "00000000-0000-0000-0000-000000000002",
  "remittanceId": "00000000-0000-0000-0000-000000000003",
  "partnerId": "00000000-0000-0000-0000-000000000001",
  "operationType": "LockFunds",
  "unsignedTransactionXdr": "<envelope-XDR-base64>",
  "expectedTransaction": {
    "network": "Testnet",
    "assetCode": "USDC",
    "assetIssuer": "<G-del-issuer-configurado>",
    "amount": 2.5,
    "destination": "<G-o-C-del-destino>"
  }
}
```

`operationType` admite `LockFunds`, `ReleaseFunds` y `RefundFunds`, que corresponden respectivamente a `lock_funds`, `release_funds` y `refund_funds`. `amount` es decimal, positivo y de como máximo 7 decimales; evita `double` y `float` en el consumidor. El límite por defecto del Signer es `10000`, configurable en `Stellar__MaxAmount`. El XDR se limita a 131072 caracteres. La clave de idempotencia no puede superar 160 caracteres.

## XDR que debe construir APP_REMITTANCES_MS

1. Obtén o crea la wallet del socio y usa su `publicKey` como source account de la transacción. Esa cuenta debe existir y estar financiada en Stellar Testnet; el Signer no la crea en cadena.
2. Construye y prepara fuera del Signer un envelope V1 **sin firmas**, con secuencia y recursos Soroban listos, sin memo, con time bounds vigentes y con una única `InvokeContractOperation`. No uses fee bump, operaciones adicionales, precondiciones extra ni entradas Soroban `auth` adicionales. La source account de la operación, si se incluye, debe coincidir con la de la transacción.
3. Invoca exactamente el contrato configurado y una de las tres funciones permitidas. El ABI **asumido por este MVP** tiene cuatro argumentos en este orden: `SCString("USDC")`, `SCString(issuer G...)`, `SCInt128(amount en unidades de 10^-7)`, `SCString(destination G... o C...)`. El `SCInt128` debe representar un importe positivo exacto; el código actual acepta su parte alta igual a cero. Verifica este ABI contra `STELLAR_ESCROW_CONTRACT` real antes de operar con fondos.
4. Envía en `expectedTransaction` los valores exactos que codificaste en el XDR. `network` debe ser exactamente `Testnet`. El Signer inspecciona el XDR y compara operación, asset, issuer, amount, destination, source account y contrato. La firma y el hash usan la passphrase oficial `Test SDF Network ; September 2015`.
5. Si la firma tiene éxito, usa `signedTransactionXdr` para transmitir la transacción desde el servicio responsable de la red. El Signer devuelve el hash de la transacción; no la transmite ni informa su confirmación en ledger.

## Reintentos, estados y errores

- Genera `requestId` e `Idempotency-Key` una vez por intento lógico de negocio y persístelos en `APP_REMITTANCES_MS`. Conserva también el XDR original y todo `expectedTransaction` para reintentar **el mismo payload**. El hash de idempotencia incluye esos datos y la identidad `client_id` del JWT (o `NameIdentifier` si no existe `client_id`); usa una identidad de cliente estable entre reintentos.
- Un POST repetido con la misma clave y payload devuelve el XDR firmado guardado, o repite el error de rechazo guardado. Reutilizar la clave o el `requestId` con contenido diferente produce `409 SIGNING_REQUEST_CONFLICT`. El GET de firma sirve para consultar estado, pero no recupera el XDR; para recuperarlo repite el POST original.
- El Signer guarda `Signed` o `Rejected` y registra auditoría. Respuestas de error conocidas usan `ProblemDetails` con el código estable en `title` y el estado HTTP en `status`: `INVALID_REQUEST`/`INVALID_TRANSACTION_XDR` (`400`), `UNAUTHENTICATED` (`401`), `FORBIDDEN` (`403`), `WALLET_NOT_FOUND`/`SIGNING_REQUEST_NOT_FOUND` (`404`), `SIGNING_REQUEST_CONFLICT` (`409`), errores de política o contexto (`422`), `RATE_LIMIT_EXCEEDED` (`429`) y `MASTER_KEY_UNAVAILABLE` (`503`). Trata cualquier fallo o timeout de transmisión a Stellar como un estado aparte de la remesa: `Signed` solo significa que el Signer produjo un XDR firmado.
- El límite HTTP por defecto es 30 peticiones por minuto por `client_id` o IP. El request body no puede superar 262144 bytes. Si envías `X-Correlation-ID` alfanumérico o con guiones de hasta 64 caracteres, el Signer lo devuelve en la respuesta.

## Guía de integración para el otro repositorio

- Implementa un cliente HTTP interno tipado con DTOs para las cuatro rutas, timeouts, JWT de servicio y manejo explícito de `ProblemDetails`. No accedas a la base de datos del Signer ni reutilices su material criptográfico.
- Persiste en `APP_REMITTANCES_MS` los UUID externos, la clave de idempotencia, el XDR original y la referencia al resultado. Decide allí cuándo crear la wallet, financiarla, preparar la invocación, solicitar la firma, transmitir y reconciliar el estado en ledger.
- Prueba con el ABI real del contrato y con Testnet. Cubre la creación repetida de wallet, reintento idéntico de firma, conflicto por payload distinto, rechazo por contexto/XDR alterado y recuperación tras fallo de red entre la firma y la transmisión.
- Para modificar este repositorio: mantiene .NET 9, PostgreSQL con EF Core 9/Npgsql y migraciones; no uses `EnsureCreated()`, SQLite ni EF InMemory para las pruebas de persistencia. Ejecuta `dotnet build APP_STELLAR_SIGNER.sln` y `dotnet test APP_STELLAR_SIGNER.sln` (las pruebas de integración necesitan Docker y PostgreSQL real). No registres XDR, JWT, secretos ni cuerpos de solicitudes.

Este `AGENTS.md` se descubre automáticamente solo al trabajar **dentro** de este repositorio. Si el agente trabaja en `APP_REMITTANCES_MS`, indícale expresamente que lea este archivo desde este repositorio antes de implementar la integración.
