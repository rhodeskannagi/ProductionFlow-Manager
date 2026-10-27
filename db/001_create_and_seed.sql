-- ProductionFlow public sample database. DO NOT execute this in any company/ERP database.
-- This script creates its own independent UretimAkisDemo database with synthetic data.
USE master;
GO
IF DB_ID(N'UretimAkisDemo') IS NULL CREATE DATABASE UretimAkisDemo;
GO
USE UretimAkisDemo;
GO
IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
CREATE TABLE dbo.Urunler (
    UrunId INT NOT NULL PRIMARY KEY, StokKodu NVARCHAR(40) NOT NULL UNIQUE,
    [Ad] NVARCHAR(120) NOT NULL, Birim NVARCHAR(12) NOT NULL,
    Tur NVARCHAR(12) NOT NULL CHECK(Tur IN ('finished','material'))
);
GO
IF OBJECT_ID(N'dbo.Recipes', N'U') IS NULL
CREATE TABLE dbo.ReceteKalemleri (
    UrunId INT NOT NULL REFERENCES dbo.Urunler(UrunId),
    MalzemeId INT NOT NULL REFERENCES dbo.Urunler(UrunId),
    BirimTuketim DECIMAL(18,4) NOT NULL CHECK (BirimTuketim > 0),
    CONSTRAINT PK_Recipes PRIMARY KEY (UrunId,MalzemeId),
    CONSTRAINT CK_RecipeDifferentProducts CHECK(UrunId<>MalzemeId)
);
GO
IF OBJECT_ID(N'dbo.StockBalances', N'U') IS NULL
CREATE TABLE dbo.StokBakiyeleri (
    UrunId INT NOT NULL PRIMARY KEY REFERENCES dbo.Urunler(UrunId),
    MevcutMiktar DECIMAL(18,4) NOT NULL DEFAULT 0 CHECK(MevcutMiktar>=0)
);
GO
IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL
CREATE TABLE dbo.UretimEmirleri (
    EmirId INT NOT NULL PRIMARY KEY, Kod NVARCHAR(24) NOT NULL UNIQUE,
    UrunId INT NOT NULL REFERENCES dbo.Urunler(UrunId),
    PlanlananMiktar DECIMAL(18,4) NOT NULL CHECK(PlanlananMiktar>0),
    UretilenMiktar DECIMAL(18,4) NOT NULL DEFAULT 0 CHECK(UretilenMiktar>=0),
    Durum NVARCHAR(16) NOT NULL DEFAULT N'Başladı' CHECK(Durum IN (N'Başladı',N'Üretiliyor',N'Kapandı')),
    OlusturmaZamani DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    SatirSurumu ROWVERSION NOT NULL,
    CONSTRAINT CK_OrderNotOverproduced CHECK(UretilenMiktar<=PlanlananMiktar)
);
GO
IF OBJECT_ID(N'dbo.ProductionRuns', N'U') IS NULL
CREATE TABLE dbo.UretimKayitlari (
    UretimId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
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
IF OBJECT_ID(N'dbo.StockMovements', N'U') IS NULL
CREATE TABLE dbo.StokHareketleri (
    HareketId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    UretimId UNIQUEIDENTIFIER NOT NULL REFERENCES dbo.UretimKayitlari(UretimId),
    UrunId INT NOT NULL REFERENCES dbo.Urunler(UrunId),
    MiktarDegisimi DECIMAL(18,4) NOT NULL CHECK(MiktarDegisimi<>0),
    HareketTuru CHAR(1) NOT NULL CHECK(HareketTuru IN ('P','C','U')),
    OlusturmaZamani DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX IX_StockMovements_RunId ON dbo.StokHareketleri(UretimId, HareketTuru);
GO
IF OBJECT_ID(N'dbo.AuditEvents', N'U') IS NULL
CREATE TABLE dbo.IslemGunlugu (
    GunlukId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    IslemZamani DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    Kullanici NVARCHAR(80) NOT NULL,
    Islem NVARCHAR(30) NOT NULL,
    UretimId UNIQUEIDENTIFIER NULL,
    Aciklama NVARCHAR(600) NOT NULL
);
GO
-- Idempotent demonstration seed: no real employee/customer/source ERP records.
IF NOT EXISTS (SELECT 1 FROM dbo.Urunler WHERE UrunId=1)
BEGIN
 INSERT dbo.Urunler(UrunId,StokKodu,[Ad],Birim,Tur) VALUES
 (1,N'YM-ALFA-120',N'Alfa Yarı Mamul Panel',N'adet',N'finished'),
 (2,N'YM-BETA-090',N'Beta Montaj Profili',N'adet',N'finished'),
 (3,N'YM-GAMA-200',N'Gama Gövde Modülü',N'adet',N'finished'),
 (4,N'HM-ALU-6063',N'Alüminyum Profil',N'kg',N'material'),
 (5,N'HM-BAG-001',N'Bağlantı Elemanı',N'adet',N'material'),
 (6,N'HM-CONTA-02',N'Sızdırmazlık Contası',N'm',N'material'),
 (7,N'HM-LEV-03',N'Alüminyum Levha',N'm²',N'material');
 INSERT dbo.ReceteKalemleri(UrunId,MalzemeId,BirimTuketim) VALUES
 (1,4,2.5),(1,5,4),(2,4,1.2),(2,6,0.7),(3,7,1.8),(3,5,8);
 INSERT dbo.StokBakiyeleri(UrunId,MevcutMiktar) VALUES
 (1,32),(2,24),(3,0),(4,800),(5,1500),(6,220),(7,210);
 INSERT dbo.UretimEmirleri(EmirId,Kod,UrunId,PlanlananMiktar,UretilenMiktar,Durum,OlusturmaZamani) VALUES
 (101,N'ÜE-260101',1,120,0,N'Başladı','2026-10-12T08:15:00'),
 (102,N'ÜE-260102',2,80,24,N'Üretiliyor','2026-10-13T09:42:00'),
 (103,N'ÜE-260103',3,50,0,N'Başladı','2026-10-14T07:30:00'),
 (104,N'ÜE-260104',1,32,32,N'Kapandı','2026-10-14T12:00:00'),
 (105,N'ÜE-260105',2,65,0,N'Başladı','2026-10-15T11:20:00');
 INSERT dbo.UretimKayitlari(UretimId,IstekId,EmirId,Miktar,OlusturmaZamani,Operator) VALUES
 ('f1111111-1111-4111-8111-111111111111','a1111111-1111-4111-8111-111111111111',102,24,'2026-10-15T10:12:00',N'Demo Operatör'),
 ('f2222222-2222-4222-8222-222222222222','a2222222-2222-4222-8222-222222222222',104,32,'2026-10-15T13:45:00',N'Demo Operatör');
 INSERT dbo.StokHareketleri(UretimId,UrunId,MiktarDegisimi,HareketTuru,OlusturmaZamani) VALUES
 ('f1111111-1111-4111-8111-111111111111',2,24,'P','2026-10-15T10:12:00'),
 ('f1111111-1111-4111-8111-111111111111',4,-28.8,'C','2026-10-15T10:12:00'),
 ('f1111111-1111-4111-8111-111111111111',6,-16.8,'C','2026-10-15T10:12:00'),
 ('f2222222-2222-4222-8222-222222222222',1,32,'P','2026-10-15T13:45:00'),
 ('f2222222-2222-4222-8222-222222222222',4,-80,'C','2026-10-15T13:45:00'),
 ('f2222222-2222-4222-8222-222222222222',5,-128,'C','2026-10-15T13:45:00');
 INSERT dbo.IslemGunlugu(IslemZamani,Kullanici,Islem,Aciklama)
 VALUES('2026-10-15T13:45:00',N'Demo Operatör',N'DEMO_SEED',N'Bağımsız, sentetik test verisi oluşturuldu.');
END;
GO
PRINT 'UretimAkisDemo initialized with synthetic records.';
