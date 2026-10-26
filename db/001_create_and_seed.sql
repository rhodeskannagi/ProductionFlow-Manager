-- ProductionFlow public sample database. DO NOT execute this in any company/ERP database.
-- This script creates its own independent ProductionFlowDemo database with synthetic data.
USE master;
GO
IF DB_ID(N'ProductionFlowDemo') IS NULL CREATE DATABASE ProductionFlowDemo;
GO
USE ProductionFlowDemo;
GO
IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
CREATE TABLE dbo.Products (
    ProductId INT NOT NULL PRIMARY KEY, Sku NVARCHAR(40) NOT NULL UNIQUE,
    [Name] NVARCHAR(120) NOT NULL, Unit NVARCHAR(12) NOT NULL,
    Kind NVARCHAR(12) NOT NULL CHECK(Kind IN ('finished','material'))
);
GO
IF OBJECT_ID(N'dbo.Recipes', N'U') IS NULL
CREATE TABLE dbo.Recipes (
    ProductId INT NOT NULL REFERENCES dbo.Products(ProductId),
    MaterialId INT NOT NULL REFERENCES dbo.Products(ProductId),
    PerUnit DECIMAL(18,4) NOT NULL CHECK (PerUnit > 0),
    CONSTRAINT PK_Recipes PRIMARY KEY (ProductId,MaterialId),
    CONSTRAINT CK_RecipeDifferentProducts CHECK(ProductId<>MaterialId)
);
GO
IF OBJECT_ID(N'dbo.StockBalances', N'U') IS NULL
CREATE TABLE dbo.StockBalances (
    ProductId INT NOT NULL PRIMARY KEY REFERENCES dbo.Products(ProductId),
    OnHand DECIMAL(18,4) NOT NULL DEFAULT 0 CHECK(OnHand>=0)
);
GO
IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL
CREATE TABLE dbo.Orders (
    OrderId INT NOT NULL PRIMARY KEY, Code NVARCHAR(24) NOT NULL UNIQUE,
    ProductId INT NOT NULL REFERENCES dbo.Products(ProductId),
    Planned DECIMAL(18,4) NOT NULL CHECK(Planned>0),
    Produced DECIMAL(18,4) NOT NULL DEFAULT 0 CHECK(Produced>=0),
    Status NVARCHAR(16) NOT NULL DEFAULT N'Başladı' CHECK(Status IN (N'Başladı',N'Üretiliyor',N'Kapandı')),
    CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    RowVer ROWVERSION NOT NULL,
    CONSTRAINT CK_OrderNotOverproduced CHECK(Produced<=Planned)
);
GO
IF OBJECT_ID(N'dbo.ProductionRuns', N'U') IS NULL
CREATE TABLE dbo.ProductionRuns (
    RunId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    RequestId UNIQUEIDENTIFIER NOT NULL UNIQUE,
    OrderId INT NOT NULL REFERENCES dbo.Orders(OrderId),
    Quantity DECIMAL(18,4) NOT NULL CHECK(Quantity>0),
    CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    ReversedAt DATETIME2(3) NULL,
    Reason NVARCHAR(300) NULL,
    Operator NVARCHAR(80) NOT NULL,
    ReversedBy NVARCHAR(80) NULL
);
GO
IF OBJECT_ID(N'dbo.StockMovements', N'U') IS NULL
CREATE TABLE dbo.StockMovements (
    MovementId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    RunId UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.ProductionRuns(RunId),
    ProductId INT NOT NULL REFERENCES dbo.Products(ProductId),
    Delta DECIMAL(18,4) NOT NULL CHECK(Delta<>0),
    MovementType CHAR(1) NOT NULL CHECK(MovementType IN ('P','C','U')),
    CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_StockMovements_RunId ON dbo.StockMovements(RunId, MovementType);
GO
IF OBJECT_ID(N'dbo.AuditEvents', N'U') IS NULL
CREATE TABLE dbo.AuditEvents (
    AuditId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    OccurredAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    Actor NVARCHAR(80) NOT NULL,
    Action NVARCHAR(30) NOT NULL,
    RunId UNIQUEIDENTIFIER NULL,
    Details NVARCHAR(600) NOT NULL
);
GO
-- Idempotent demonstration seed: no real employee/customer/source ERP records.
IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE ProductId=1)
BEGIN
 INSERT dbo.Products(ProductId,Sku,[Name],Unit,Kind) VALUES
 (1,N'YM-ALFA-120',N'Alfa Yarı Mamul Panel',N'adet',N'finished'),
 (2,N'YM-BETA-090',N'Beta Montaj Profili',N'adet',N'finished'),
 (3,N'YM-GAMA-200',N'Gama Gövde Modülü',N'adet',N'finished'),
 (4,N'HM-ALU-6063',N'Alüminyum Profil',N'kg',N'material'),
 (5,N'HM-BAG-001',N'Bağlantı Elemanı',N'adet',N'material'),
 (6,N'HM-CONTA-02',N'Sızdırmazlık Contası',N'm',N'material'),
 (7,N'HM-LEV-03',N'Alüminyum Levha',N'm²',N'material');
 INSERT dbo.Recipes(ProductId,MaterialId,PerUnit) VALUES
 (1,4,2.5),(1,5,4),(2,4,1.2),(2,6,0.7),(3,7,1.8),(3,5,8);
 INSERT dbo.StockBalances(ProductId,OnHand) VALUES
 (1,32),(2,24),(3,0),(4,800),(5,1500),(6,220),(7,210);
 INSERT dbo.Orders(OrderId,Code,ProductId,Planned,Produced,Status,CreatedAt) VALUES
 (101,N'ÜE-260101',1,120,0,N'Başladı','2026-10-12T08:15:00'),
 (102,N'ÜE-260102',2,80,24,N'Üretiliyor','2026-10-13T09:42:00'),
 (103,N'ÜE-260103',3,50,0,N'Başladı','2026-10-14T07:30:00'),
 (104,N'ÜE-260104',1,32,32,N'Kapandı','2026-10-14T12:00:00'),
 (105,N'ÜE-260105',2,65,0,N'Başladı','2026-10-15T11:20:00');
 INSERT dbo.ProductionRuns(RunId,RequestId,OrderId,Quantity,CreatedAt,Operator) VALUES
 ('f1111111-1111-4111-8111-111111111111','a1111111-1111-4111-8111-111111111111',102,24,'2026-10-15T10:12:00',N'Demo Operatör'),
 ('f2222222-2222-4222-8222-222222222222','a2222222-2222-4222-8222-222222222222',104,32,'2026-10-15T13:45:00',N'Demo Operatör');
 INSERT dbo.StockMovements(RunId,ProductId,Delta,MovementType,CreatedAt) VALUES
 ('f1111111-1111-4111-8111-111111111111',2,24,'P','2026-10-15T10:12:00'),
 ('f1111111-1111-4111-8111-111111111111',4,-28.8,'C','2026-10-15T10:12:00'),
 ('f1111111-1111-4111-8111-111111111111',6,-16.8,'C','2026-10-15T10:12:00'),
 ('f2222222-2222-4222-8222-222222222222',1,32,'P','2026-10-15T13:45:00'),
 ('f2222222-2222-4222-8222-222222222222',4,-80,'C','2026-10-15T13:45:00'),
 ('f2222222-2222-4222-8222-222222222222',5,-128,'C','2026-10-15T13:45:00');
 INSERT dbo.AuditEvents(OccurredAt,Actor,Action,Details)
 VALUES('2026-10-15T13:45:00',N'Demo Operatör',N'DEMO_SEED',N'Bağımsız, sentetik test verisi oluşturuldu.');
END;
GO
PRINT 'ProductionFlowDemo initialized with synthetic records.';
