using StellarSigner.Domain.Enums;
namespace StellarSigner.Application.Abstractions.Blockchain;
public sealed record InspectedTransaction(string TransactionHash, string SourceAccount, string ContractId, string FunctionName,
    SigningOperationType OperationType, string AssetCode, string AssetIssuer, decimal Amount, string Destination,
    DateTimeOffset Expiration, int OperationCount);
public interface ITransactionDecoder { InspectedTransaction Decode(string xdr, string networkPassphrase); }
