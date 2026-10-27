-- UretimAkisDemo: READ-ONLY checks. No UPDATE, DELETE, or schema mutations.
-- Run only after db/001_create_and_seed.sql on the synthetic DEMO database.
USE UretimAkisDemo;
GO

-- 1: Order amounts and status must match active production runs.
SELECT o.EmirId,o.Kod,o.PlanlananMiktar,o.UretilenMiktar,o.Durum,
       COALESCE(SUM(CASE WHEN r.GeriAlmaZamani IS NULL THEN r.Miktar ELSE 0 END),0) AS ActiveRunsTotal,
       CASE WHEN o.UretilenMiktar <> COALESCE(SUM(CASE WHEN r.GeriAlmaZamani IS NULL THEN r.Miktar ELSE 0 END),0)
                THEN 'ORDER_QUANTITY'
            WHEN o.Durum <> CASE WHEN o.UretilenMiktar <= 0 THEN N'Başladı'
                                 WHEN o.UretilenMiktar >= o.PlanlananMiktar THEN N'Kapandı'
                                 ELSE N'Üretiliyor' END THEN 'ORDER_STATUS'
            ELSE 'OK' END AS CheckResult
FROM dbo.UretimEmirleri o LEFT JOIN dbo.UretimKayitlari r ON r.EmirId=o.EmirId
GROUP BY o.EmirId,o.Kod,o.PlanlananMiktar,o.UretilenMiktar,o.Durum
ORDER BY o.EmirId;
GO

-- 2: Output movements must match the quantity of the run.
SELECT r.UretimId,o.Kod,r.Miktar,
       COALESCE(SUM(CASE WHEN m.HareketTuru='P' AND m.UrunId=o.UrunId THEN m.MiktarDegisimi ELSE 0 END),0) AS RecordedOutput,
       SUM(CASE WHEN m.HareketTuru='P' AND m.UrunId=o.UrunId THEN 1 ELSE 0 END) AS OutputMovementCount,
       CASE WHEN r.Miktar <> COALESCE(SUM(CASE WHEN m.HareketTuru='P' AND m.UrunId=o.UrunId THEN m.MiktarDegisimi ELSE 0 END),0)
                  OR SUM(CASE WHEN m.HareketTuru='P' AND m.UrunId=o.UrunId THEN 1 ELSE 0 END)<>1
             THEN 'RUN_OUTPUT' ELSE 'OK' END AS CheckResult
FROM dbo.UretimKayitlari r JOIN dbo.UretimEmirleri o ON o.EmirId=r.EmirId
LEFT JOIN dbo.StokHareketleri m ON m.UretimId=r.UretimId
GROUP BY r.UretimId,o.Kod,r.Miktar,o.UrunId;
GO

-- 3: Raw material use must match the recipe for each run.
SELECT r.UretimId,o.Kod,p.StokKodu, r.Miktar*rec.BirimTuketim AS RecipeConsumption,
       -COALESCE(SUM(CASE WHEN m.HareketTuru='C' THEN m.MiktarDegisimi ELSE 0 END),0) AS RecordedConsumption,
       CASE WHEN r.Miktar*rec.BirimTuketim <> -COALESCE(SUM(CASE WHEN m.HareketTuru='C' THEN m.MiktarDegisimi ELSE 0 END),0)
             THEN 'BOM_CONSUMPTION' ELSE 'OK' END AS CheckResult
FROM dbo.UretimKayitlari r JOIN dbo.UretimEmirleri o ON o.EmirId=r.EmirId
JOIN dbo.ReceteKalemleri rec ON rec.UrunId=o.UrunId
JOIN dbo.Urunler p ON p.UrunId=rec.MalzemeId
LEFT JOIN dbo.StokHareketleri m ON m.UretimId=r.UretimId AND m.UrunId=rec.MalzemeId
GROUP BY r.UretimId,o.Kod,p.StokKodu,r.Miktar,rec.BirimTuketim;
GO

-- 4: Undo ledger must cancel originals exactly, grouped per SKU.
SELECT r.UretimId,p.StokKodu,r.GeriAlmaZamani,
       SUM(CASE WHEN m.HareketTuru IN ('P','C') THEN m.MiktarDegisimi ELSE 0 END) AS OriginalDelta,
       SUM(CASE WHEN m.HareketTuru='U' THEN m.MiktarDegisimi ELSE 0 END) AS ReversalDelta,
       CASE WHEN r.GeriAlmaZamani IS NOT NULL AND SUM(m.MiktarDegisimi)<>0 THEN 'REVERSAL_LEDGER'
            WHEN r.GeriAlmaZamani IS NULL AND SUM(CASE WHEN m.HareketTuru='U' THEN m.MiktarDegisimi ELSE 0 END)<>0 THEN 'REVERSAL_LEDGER'
            ELSE 'OK' END AS CheckResult
FROM dbo.StokHareketleri m JOIN dbo.UretimKayitlari r ON r.UretimId=m.UretimId
JOIN dbo.Urunler p ON p.UrunId=m.UrunId
GROUP BY r.UretimId,p.StokKodu,r.GeriAlmaZamani;
GO

-- 5: Operational warnings: undo is unavailable for a run if FG was consumed.
SELECT r.UretimId,o.Kod,b.MevcutMiktar AS FinishedOnHand,r.Miktar AS UndoNeeds,'UNDO_BLOCKED' AS Warning
FROM dbo.UretimKayitlari r JOIN dbo.UretimEmirleri o ON o.EmirId=r.EmirId
JOIN dbo.StokBakiyeleri b ON b.UrunId=o.UrunId
WHERE r.GeriAlmaZamani IS NULL AND b.MevcutMiktar < r.Miktar;
GO
