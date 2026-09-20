using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StellarDotnetSdk.Accounts;
using StellarDotnetSdk;
using StellarDotnetSdk.Operations;
using StellarDotnetSdk.Soroban;
using StellarDotnetSdk.Transactions;
using StellarSigner.Domain.Entities;
using StellarSigner.Domain.Enums;
using StellarSigner.Application.Abstractions.Blockchain;
using StellarSigner.Application.Abstractions.Cryptography;
using StellarSigner.Application.Abstractions.Security;
using StellarSigner.Application.Common;
using StellarSigner.Application.Signatures;
using StellarSigner.Application.Signatures.Commands.SignTransaction;
using StellarSigner.Infrastructure.DrivenAdapter.SigningPolicies;
using StellarSigner.Infrastructure.DrivenAdapter.Cryptography;
using StellarSigner.Infrastructure.DrivenAdapter.Stellar;
using StellarSigner.Infrastructure.DrivenAdapter.Persistence;
using StellarSigner.Infrastructure.DrivenAdapter.Persistence.Repositories;
using Testcontainers.PostgreSql;
namespace StellarSigner.IntegrationTests;
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17.6")
        .WithDatabase("stellar_signer_test")
        .WithUsername("signer")
        .WithPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(18)))
        .Build();
    public string ConnectionString => _container.GetConnectionString();
    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateDb();
        await db.Database.MigrateAsync();
    }
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
    public SignerDbContext CreateDb() => new(new DbContextOptionsBuilder<SignerDbContext>().UseNpgsql(ConnectionString).Options);
}
public sealed class PostgresPersistenceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static string Public(long index) => KeyPair.FromSecretSeed(SHA256.HashData(Encoding.UTF8.GetBytes(index.ToString()))).AccountId;
    [Fact]
    public async Task MigrationCreatesTablesHistoryAndServerGeneratedIds()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM pg_tables WHERE schemaname='public' AND tablename IN ('partner_wallets','wallet_derivation_counters','signing_requests','audit_records','__EFMigrationsHistory')", connection);
        Assert.Equal(5L, await cmd.ExecuteScalarAsync());
        await using var defaults = new NpgsqlCommand("SELECT count(*) FROM information_schema.columns WHERE table_schema='public' AND column_name='id' AND column_default='gen_random_uuid()'", connection);
        Assert.Equal(4L, await defaults.ExecuteScalarAsync());
        await using var history = new NpgsqlCommand("SELECT count(*) FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" LIKE '%_InitialCreate'", connection);
        Assert.Equal(1L, await history.ExecuteScalarAsync());
    }
    [Fact]
    public async Task ConcurrentReservationsAreUniqueAndSamePartnerIsIdempotent()
    {
        var partners = Enumerable.Range(0, 16).Select(_ => Guid.NewGuid()).ToArray();
        var tasks = partners.Select(async partner => {
            await using var db = fixture.CreateDb();
            return await new PartnerWalletRepository(db).GetOrCreateAsync(partner, Public, CancellationToken.None);
        });
        var wallets = await Task.WhenAll(tasks);
        Assert.Equal(16, wallets.Select(w => w.DerivationIndex).Distinct().Count());
        Assert.All(wallets, w => Assert.NotEqual(Guid.Empty, w.Id));
        var same = Guid.NewGuid();
        var repeated = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ => {
            await using var db = fixture.CreateDb();
            return await new PartnerWalletRepository(db).GetOrCreateAsync(same, Public, CancellationToken.None);
        }));
        Assert.Single(repeated.Select(w => w.Id).Distinct());
        Assert.Single(repeated.Select(w => w.DerivationIndex).Distinct());
    }
    [Fact]
    public async Task UniqueConstraintsSigningPersistenceAndAppendOnlyAudit()
    {
        var partner = Guid.NewGuid();
        await using var db = fixture.CreateDb();
        var wallet = await new PartnerWalletRepository(db).GetOrCreateAsync(partner, Public, CancellationToken.None);
        await using (var duplicateDb = fixture.CreateDb())
        {
            duplicateDb.PartnerWallets.Add(PartnerWallet.Create(partner, Public(wallet.DerivationIndex + 100000), wallet.DerivationIndex + 100000));
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateDb.SaveChangesAsync());
        }
        var requestId = Guid.NewGuid();
        var request = SigningRequest.Create(requestId, Guid.NewGuid().ToString("N"), new string('a',64), Guid.NewGuid(), wallet,
            new string('b',64), new string('c',64), SigningOperationType.LockFunds, "USDC", Public(9999), 12.1234567m, Public(9998));
        request.MarkSigned("signed-xdr", new string('d',64));
        db.SigningRequests.Add(request);
        var audit = AuditRecord.ForSigning(requestId, request.RemittanceId, partner, wallet.PublicKey, request.TransactionHash,
            SigningOperationType.LockFunds, request.Amount, "USDC", request.Destination, "Signed", null, "integration-test");
        db.AuditRecords.Add(audit);
        await db.SaveChangesAsync();
        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.NotEqual(Guid.Empty, audit.Id);
        await using var check = fixture.CreateDb();
        Assert.Equal("signed-xdr", (await check.SigningRequests.SingleAsync(x => x.RequestId == requestId)).SignedXdr);
        Assert.Equal(12.1234567m, (await check.AuditRecords.SingleAsync(x => x.Id == audit.Id)).Amount);
        check.Entry(await check.AuditRecords.SingleAsync(x => x.Id == audit.Id)).State = EntityState.Deleted;
        await Assert.ThrowsAsync<InvalidOperationException>(() => check.SaveChangesAsync());
        await using var duplicateRequestDb = fixture.CreateDb();
        duplicateRequestDb.SigningRequests.Add(SigningRequest.Create(Guid.NewGuid(), request.IdempotencyKey, new string('e',64),
            Guid.NewGuid(), wallet, new string('f',64), new string('1',64), SigningOperationType.LockFunds, "USDC", Public(9999), 1m, Public(9998)));
        await Assert.ThrowsAsync<DbUpdateException>(() => duplicateRequestDb.SaveChangesAsync());
    }
    private sealed class FakeDerivation(string publicKey) : IKeyDerivationService
    { public DerivedKey Derive(uint index) => new(new byte[32], publicKey); }
    private sealed class FakeSigner : ITransactionSigner
    {
        public int Calls;
        public string Sign(string unsignedXdr, byte[] privateSeed, string networkPassphrase)
        { Interlocked.Increment(ref Calls); return "signed-envelope"; }
    }
    private sealed class FakeDecoder(InspectedTransaction inspected) : ITransactionDecoder
    { public InspectedTransaction Decode(string xdr, string network) => inspected; }
    [Fact]
    public async Task ConcurrentSigningUsesOneSignatureAndConflictingPayloadIsRejected()
    {
        var partner = Guid.NewGuid();
        await using (var seedDb = fixture.CreateDb())
            await new PartnerWalletRepository(seedDb).GetOrCreateAsync(partner, Public, CancellationToken.None);
        await using var lookup = fixture.CreateDb();
        var wallet = await new PartnerWalletRepository(lookup).GetAsync(partner, CancellationToken.None);
        Assert.NotNull(wallet);
        var issuer = Public(87654); var destination = Public(87655); var contract = "contract-for-test";
        var inspected = new InspectedTransaction(new string('a',64), wallet.PublicKey, contract, "lock_funds",
            SigningOperationType.LockFunds, "USDC", issuer, 2.5m, destination, DateTimeOffset.UtcNow.AddMinutes(5), 1);
        var decoder = new FakeDecoder(inspected); var signer = new FakeSigner();
        var config = new SigningConfiguration { Issuer = issuer, ContractId = contract, MaxAmount = 10m };
        ISigningPolicy[] policies = [new AllowedAssetPolicy(), new AllowedDestinationPolicy(), new NetworkPassphrasePolicy(),
            new TransactionExpirationPolicy(), new TransactionLimitPolicy(), new AllowedContractPolicy(),
            new AllowedFunctionPolicy(), new SourceAccountPolicy(), new OperationCountPolicy()];
        var command = new SignTransactionCommand(Guid.NewGuid(), Guid.NewGuid(), partner, SigningOperationType.LockFunds,
            "unsigned-envelope", new ExpectedTransaction("Testnet", "USDC", issuer, 2.5m, destination), Guid.NewGuid().ToString("N"), "test-caller");
        async Task<SignTransactionResult> Execute(SignTransactionCommand cmd)
        {
            await using var db = fixture.CreateDb();
            var handler = new SignTransactionHandler(new PartnerWalletRepository(db), new SigningRequestRepository(db),
                new AuditRepository(db), db, decoder, policies, new FakeDerivation(wallet.PublicKey), signer, config);
            return await handler.Handle(cmd, CancellationToken.None);
        }
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Execute(command)));
        Assert.All(results, x => Assert.Equal("signed-envelope", x.SignedTransactionXdr));
        Assert.Equal(1, signer.Calls);
        await Assert.ThrowsAsync<SignerException>(() => Execute(command with
            { ExpectedTransaction = command.ExpectedTransaction with { Amount = 3m } }));
        await Assert.ThrowsAsync<SignerException>(() => Execute(command with { IdempotencyKey = Guid.NewGuid().ToString("N") }));
        await using var check = fixture.CreateDb();
        Assert.Single(await check.SigningRequests.Where(x => x.RequestId == command.RequestId).ToListAsync());
        var records = await check.AuditRecords.Where(x => x.RequestId == command.RequestId).ToListAsync();
        Assert.Equal(3, records.Count);
        Assert.Contains(records, x => x.Result == "Signed");
        Assert.Contains(records, x => x.FailureCode == "SIGNING_REQUEST_CONFLICT");
    }
    private sealed class VectorMaster : IMasterKeyProvider
    {
        public MasterKeyMaterial Load()
        {
            var seed = Convert.FromHexString("e4a5a632e70943ae7f07659df1332160937fad82587216a4c64315a0fb39497ee4a01f76ddab4cba68147977f3a147b6ad584c41808e8238a07f6cc4b582f186");
            var digest = HMACSHA512.HashData(Encoding.ASCII.GetBytes("ed25519 seed"), seed);
            CryptographicOperations.ZeroMemory(seed);
            var material = new MasterKeyMaterial(digest[..32], digest[32..]);
            CryptographicOperations.ZeroMemory(digest);
            return material;
        }
        public bool IsAvailable() => true;
    }
    [Fact]
    public async Task RealDerivationXdrInspectionSigningAndRejectionWorkWithPostgres()
    {
        var partner = Guid.NewGuid();
        var derivation = new Slip10KeyDerivationService(new VectorMaster());
        PartnerWallet wallet;
        await using (var walletDb = fixture.CreateDb())
            wallet = await new PartnerWalletRepository(walletDb).GetOrCreateAsync(partner, index => {
                using var key = derivation.Derive((uint)index); return key.PublicKey;
            }, CancellationToken.None);
        var issuer = KeyPair.Random().AccountId;
        var destination = KeyPair.Random().AccountId;
        var contract = StrKey.EncodeContractId(RandomNumberGenerator.GetBytes(32));
        SCVal[] args = [new SCString("USDC"), new SCString(issuer), new SCInt128(0, 25000000), new SCString(destination)];
        string Envelope(bool extra)
        {
            var builder = new TransactionBuilder(new Account(wallet.PublicKey, 10))
                .AddOperation(new InvokeContractOperation(contract, "lock_funds", args, null))
                .AddTimeBounds(new TimeBounds(DateTimeOffset.UtcNow.AddSeconds(-5), TimeSpan.FromMinutes(3)))
                .SetSorobanTransactionData(new SorobanTransactionData(new SorobanResources(new LedgerFootprint(), 1000, 1000, 1000),
                    100, new SorobanResourceExtensionV0([])));
            if (extra) builder.AddOperation(new InvokeContractOperation(contract, "lock_funds", args, null));
            return builder.Build().ToUnsignedEnvelopeXdrBase64(TransactionBase.TransactionXdrVersion.V1);
        }
        var config = new SigningConfiguration { Issuer = issuer, ContractId = contract, MaxAmount = 10m };
        ISigningPolicy[] policies = [new AllowedAssetPolicy(), new AllowedDestinationPolicy(), new NetworkPassphrasePolicy(),
            new TransactionExpirationPolicy(), new TransactionLimitPolicy(), new AllowedContractPolicy(),
            new AllowedFunctionPolicy(), new SourceAccountPolicy(), new OperationCountPolicy()];
        async Task<SignTransactionResult> Execute(SignTransactionCommand cmd)
        {
            await using var db = fixture.CreateDb();
            return await new SignTransactionHandler(new PartnerWalletRepository(db), new SigningRequestRepository(db),
                new AuditRepository(db), db, new StellarTransactionDecoder(), policies, derivation,
                new Ed25519TransactionSigner(), config).Handle(cmd, CancellationToken.None);
        }
        var command = new SignTransactionCommand(Guid.NewGuid(), Guid.NewGuid(), partner, SigningOperationType.LockFunds,
            Envelope(false), new ExpectedTransaction("Testnet", "USDC", issuer, 2.5m, destination),
            Guid.NewGuid().ToString("N"), "integration-caller");
        var signed = await Execute(command);
        Assert.Single(Transaction.FromEnvelopeXdr(signed.SignedTransactionXdr).Signatures);
        Assert.Equal(signed.SignedTransactionXdr, (await Execute(command)).SignedTransactionXdr);
        var manipulated = command with { RequestId = Guid.NewGuid(), IdempotencyKey = Guid.NewGuid().ToString("N"), UnsignedTransactionXdr = Envelope(true) };
        Assert.Equal("INVALID_TRANSACTION_XDR", (await Assert.ThrowsAsync<SignerException>(() => Execute(manipulated))).Code);
        Assert.Equal("INVALID_TRANSACTION_XDR", (await Assert.ThrowsAsync<SignerException>(() => Execute(manipulated))).Code);
        var wrongNetwork = command with { RequestId = Guid.NewGuid(), IdempotencyKey = Guid.NewGuid().ToString("N"),
            ExpectedTransaction = command.ExpectedTransaction with { Network = "Public" } };
        Assert.Equal("NETWORK_MISMATCH", (await Assert.ThrowsAsync<SignerException>(() => Execute(wrongNetwork))).Code);
        await using var check = fixture.CreateDb();
        Assert.Equal(SigningStatus.Rejected, (await check.SigningRequests.SingleAsync(x => x.RequestId == manipulated.RequestId)).Status);
        Assert.Equal(3, await check.AuditRecords.CountAsync(x => x.PartnerId == partner &&
            (x.RequestId == command.RequestId || x.RequestId == manipulated.RequestId || x.RequestId == wrongNetwork.RequestId)));
    }
}
