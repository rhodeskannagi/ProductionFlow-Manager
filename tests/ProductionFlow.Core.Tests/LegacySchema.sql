CREATE TABLE dbo.Products (
    ProductId INT IDENTITY(1,1) NOT NULL PRIMARY KEY, Sku NVARCHAR(40) NOT NULL UNIQUE,
    [Name] NVARCHAR(120) NOT NULL, Unit NVARCHAR(12) NOT NULL,
    Kind NVARCHAR(12) NOT NULL CHECK(Kind IN ('finished','material'))
);
GO
CREATE TABLE dbo.Recipes (
    ProductId INT NOT NULL REFERENCES dbo.Products(ProductId),
    MaterialId INT NOT NULL REFERENCES dbo.Products(ProductId),
    PerUnit DECIMAL(18,4) NOT NULL CHECK (PerUnit > 0),
    CONSTRAINT PK_Recipes PRIMARY KEY (ProductId,MaterialId),
    CONSTRAINT CK_RecipeDifferentProducts CHECK(ProductId<>MaterialId)
);
GO
CREATE TABLE dbo.StockBalances (
    ProductId INT NOT NULL PRIMARY KEY REFERENCES dbo.Products(ProductId),
    OnHand DECIMAL(18,4) NOT NULL DEFAULT 0 CHECK(OnHand>=0)
);
GO
CREATE TABLE dbo.Orders (
    OrderId INT IDENTITY(1,1) NOT NULL PRIMARY KEY, Code NVARCHAR(24) NOT NULL UNIQUE,
    ProductId INT NOT NULL REFERENCES dbo.Products(ProductId),
    Planned DECIMAL(18,4) NOT NULL CHECK(Planned>0),
    Produced DECIMAL(18,4) NOT NULL DEFAULT 0 CHECK(Produced>=0),
    Status NVARCHAR(16) NOT NULL DEFAULT N'Başladı' CHECK(Status IN (N'Başladı',N'Üretiliyor',N'Kapandı')),
    CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    RowVer ROWVERSION NOT NULL,
    CONSTRAINT CK_OrderNotOverproduced CHECK(Produced<=Planned)
);
GO
CREATE TABLE dbo.ProductionRuns (
    RunId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY NONCLUSTERED,
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
CREATE TABLE dbo.StockMovements (
    MovementId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    RunId UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.ProductionRuns(RunId),
    ProductId INT NOT NULL REFERENCES dbo.Products(ProductId),
    Delta DECIMAL(18,4) NOT NULL CHECK(Delta<>0),
    MovementType CHAR(1) NOT NULL CHECK(MovementType IN ('P','C','U')),
    CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_StockMovements_RunId ON dbo.StockMovements(RunId, MovementType, ProductId) INCLUDE (Delta);
GO
CREATE TABLE dbo.AuditEvents (
    AuditId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    OccurredAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    Actor NVARCHAR(80) NOT NULL,
    Action NVARCHAR(30) NOT NULL,
    RunId UNIQUEIDENTIFIER NULL,
    Details NVARCHAR(600) NOT NULL
);
GO

CREATE UNIQUE CLUSTERED INDEX CX_ProductionRuns_CreatedAt ON dbo.ProductionRuns(CreatedAt, RunId);
CREATE INDEX IX_ProductionRuns_Order_Reversed ON dbo.ProductionRuns(OrderId,ReversedAt) INCLUDE (Quantity);
CREATE INDEX IX_Orders_Product ON dbo.Orders(ProductId) INCLUDE (Planned,Produced,Status);
CREATE INDEX IX_Recipes_Material ON dbo.Recipes(MaterialId);
CREATE TABLE dbo.InventoryAdjustments (
 AdjustmentId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
 ProductId INT NOT NULL REFERENCES dbo.Products(ProductId),
 Delta DECIMAL(18,4) NOT NULL CHECK(Delta<>0),
 Reason NVARCHAR(300) NOT NULL,
 Actor NVARCHAR(80) NOT NULL,
 CreatedAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_InventoryAdjustments_Product ON dbo.InventoryAdjustments(ProductId,CreatedAt) INCLUDE(Delta);
CREATE TABLE dbo.AppSchema (
 Id INT NOT NULL CONSTRAINT PK_AppSchema PRIMARY KEY CHECK(Id=1),
 ApplicationId UNIQUEIDENTIFIER NOT NULL,
 Version INT NOT NULL,
 InstalledAt DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);
INSERT dbo.AppSchema(Id,ApplicationId,Version) VALUES (1,'F8F82B35-62A6-4932-B02B-893981A2C860',1);
