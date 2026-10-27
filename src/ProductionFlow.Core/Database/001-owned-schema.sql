CREATE TABLE dbo.Urunler (
    UrunId INT IDENTITY(1,1) NOT NULL PRIMARY KEY, StokKodu NVARCHAR(40) NOT NULL UNIQUE,
    [Ad] NVARCHAR(120) NOT NULL, Birim NVARCHAR(12) NOT NULL,
    Tur NVARCHAR(12) NOT NULL CHECK(Tur IN ('finished','material'))
);
GO
CREATE TABLE dbo.ReceteKalemleri (
    UrunId INT NOT NULL REFERENCES dbo.Urunler(UrunId),
    MalzemeId INT NOT NULL REFERENCES dbo.Urunler(UrunId),
    BirimTuketim DECIMAL(18,4) NOT NULL CHECK (BirimTuketim > 0),
    CONSTRAINT PK_Recipes PRIMARY KEY (UrunId,MalzemeId),
    CONSTRAINT CK_RecipeDifferentProducts CHECK(UrunId<>MalzemeId)
);
GO
CREATE TABLE dbo.StokBakiyeleri (
    UrunId INT NOT NULL PRIMARY KEY REFERENCES dbo.Urunler(UrunId),
    MevcutMiktar DECIMAL(18,4) NOT NULL DEFAULT 0 CHECK(MevcutMiktar>=0)
);
GO
CREATE TABLE dbo.UretimEmirleri (
    EmirId INT IDENTITY(1,1) NOT NULL PRIMARY KEY, Kod NVARCHAR(24) NOT NULL UNIQUE,
    UrunId INT NOT NULL REFERENCES dbo.Urunler(UrunId),
    PlanlananMiktar DECIMAL(18,4) NOT NULL CHECK(PlanlananMiktar>0),
    UretilenMiktar DECIMAL(18,4) NOT NULL DEFAULT 0 CHECK(UretilenMiktar>=0),
    Durum NVARCHAR(16) NOT NULL DEFAULT N'Başladı' CHECK(Durum IN (N'Başladı',N'Üretiliyor',N'Kapandı')),
    OlusturmaZamani DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    SatirSurumu ROWVERSION NOT NULL,
    CONSTRAINT CK_OrderNotOverproduced CHECK(UretilenMiktar<=PlanlananMiktar)
);
GO
CREATE TABLE dbo.UretimKayitlari (
    UretimId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY NONCLUSTERED,
    IstekId UNIQUEIDENTIFIER NOT NULL UNIQUE,
    EmirId INT NOT NULL REFERENCES dbo.UretimEmirleri(EmirId),
    Miktar DECIMAL(18,4) NOT NULL CHECK(Miktar>0),
    OlusturmaZamani DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    GeriAlmaZamani DATETIME2(3) NULL,
    Gerekce NVARCHAR(300) NULL,
    Operator NVARCHAR(80) NOT NULL,
    GeriAlan NVARCHAR(80) NULL
);
GO
CREATE TABLE dbo.StokHareketleri (
    HareketId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UretimId UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.UretimKayitlari(UretimId),
    UrunId INT NOT NULL REFERENCES dbo.Urunler(UrunId),
    MiktarDegisimi DECIMAL(18,4) NOT NULL CHECK(MiktarDegisimi<>0),
    HareketTuru CHAR(1) NOT NULL CHECK(HareketTuru IN ('P','C','U')),
    OlusturmaZamani DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_StockMovements_RunId ON dbo.StokHareketleri(UretimId, HareketTuru, UrunId) INCLUDE (MiktarDegisimi);
GO
CREATE TABLE dbo.IslemGunlugu (
    GunlukId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    IslemZamani DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    Kullanici NVARCHAR(80) NOT NULL,
    Islem NVARCHAR(30) NOT NULL,
    UretimId UNIQUEIDENTIFIER NULL,
    Aciklama NVARCHAR(600) NOT NULL
);
GO

CREATE UNIQUE CLUSTERED INDEX CX_ProductionRuns_CreatedAt ON dbo.UretimKayitlari(OlusturmaZamani, UretimId);
CREATE INDEX IX_ProductionRuns_Order_Reversed ON dbo.UretimKayitlari(EmirId,GeriAlmaZamani) INCLUDE (Miktar);
CREATE INDEX IX_Orders_Product ON dbo.UretimEmirleri(UrunId) INCLUDE (PlanlananMiktar,UretilenMiktar,Durum);
CREATE INDEX IX_Recipes_Material ON dbo.ReceteKalemleri(MalzemeId);
CREATE TABLE dbo.StokDuzeltmeleri (
 DuzeltmeId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
 UrunId INT NOT NULL REFERENCES dbo.Urunler(UrunId),
 MiktarDegisimi DECIMAL(18,4) NOT NULL CHECK(MiktarDegisimi<>0),
 Gerekce NVARCHAR(300) NOT NULL,
 Kullanici NVARCHAR(80) NOT NULL,
 OlusturmaZamani DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_InventoryAdjustments_Product ON dbo.StokDuzeltmeleri(UrunId,OlusturmaZamani) INCLUDE(MiktarDegisimi);
CREATE TABLE dbo.UygulamaSemasi (
 Id INT NOT NULL CONSTRAINT PK_AppSchema PRIMARY KEY CHECK(Id=1),
 UygulamaKimligi UNIQUEIDENTIFIER NOT NULL,
 Surum INT NOT NULL,
 KurulumZamani DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);
INSERT dbo.UygulamaSemasi(Id,UygulamaKimligi,Surum) VALUES (1,'F8F82B35-62A6-4932-B02B-893981A2C860',2);
