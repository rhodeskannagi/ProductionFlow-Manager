using Microsoft.Data.SqlClient;

namespace ProductionFlow.Api.Services;

public sealed record IntegrityIssue(string Severity, string Code, string Reference, string Details);
public sealed record IntegrityReport(DateTime CheckedAt, int CheckedOrders, int CheckedRuns,
    IReadOnlyList<IntegrityIssue> Issues, int Errors, int Warnings);

/// <summary>
/// Read-only controls. This does not try to reconstruct opening inventory from
/// production movements: purchase, shipment and opening-balance events are out of scope.
/// </summary>
public sealed class IntegrityService(ProductionDb database)
{
    public async Task<IntegrityReport> GetReportAsync(CancellationToken ct)
    {
        await using var conn = await database.OpenAsync(ct);
        await using var tx=(SqlTransaction)await conn.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var problems = new List<IntegrityIssue>();
        var orders = 0;
        var runs = 0;
        static bool Diff(decimal x, decimal y) => Math.Abs(x - y) > 0.00001m;
        void Add(string severity, string code, string reference, string details) =>
            problems.Add(new IntegrityIssue(severity, code, reference, details));

        // Every order is compared with its active, non-reversed production runs.
        await using (var cmd = ProductionDb.Command(conn, tx, """
            SELECT o.Kod,o.PlanlananMiktar,o.UretilenMiktar,o.Durum,
                   COALESCE(SUM(CASE WHEN r.GeriAlmaZamani IS NULL THEN r.Miktar ELSE 0 END),0)
            FROM dbo.UretimEmirleri o LEFT JOIN dbo.UretimKayitlari r ON r.EmirId=o.EmirId
            GROUP BY o.EmirId,o.Kod,o.PlanlananMiktar,o.UretilenMiktar,o.Durum
            ORDER BY o.EmirId;
            """))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
            {
                orders++;
                var code = reader.GetString(0);
                var planned = reader.GetDecimal(1);
                var produced = reader.GetDecimal(2);
                var status = reader.GetString(3);
                var posted = reader.GetDecimal(4);
                if (Diff(produced, posted)) Add("error", "ORDER_QUANTITY", code,
                    $"Aktif üretim toplamı {posted}; emirde kayıtlı {produced}.");
                var expectedStatus = produced <= 0 ? "Başladı" : produced >= planned ? "Kapandı" : "Üretiliyor";
                if (status != expectedStatus) Add("error", "ORDER_STATUS", code,
                    $"Beklenen durum {expectedStatus}, kayıtlı {status}.");
            }

        await using (var cmd = ProductionDb.Command(conn, tx, """
            SELECT p.StokKodu,b.MevcutMiktar FROM dbo.StokBakiyeleri b
            JOIN dbo.Urunler p ON p.UrunId=b.UrunId WHERE b.MevcutMiktar < 0;
            """))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                Add("error", "INVALID_STOCK", reader.GetString(0), "Stok bakiyesi negatif veya geçersiz.");

        // A production run must have exactly one finished-goods receipt of its quantity.
        await using (var cmd = ProductionDb.Command(conn, tx, """
            SELECT r.UretimId,r.Miktar,
                   COALESCE(SUM(CASE WHEN m.HareketTuru='P' AND m.UrunId=o.UrunId THEN m.MiktarDegisimi ELSE 0 END),0),
                   COALESCE(SUM(CASE WHEN m.HareketTuru='P' AND m.UrunId=o.UrunId THEN 1 ELSE 0 END),0)
            FROM dbo.UretimKayitlari r JOIN dbo.UretimEmirleri o ON o.EmirId=r.EmirId
            LEFT JOIN dbo.StokHareketleri m ON m.UretimId=r.UretimId
            GROUP BY r.UretimId,r.Miktar,o.UrunId;
            """))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
            {
                runs++;
                var id = reader.GetGuid(0).ToString();
                var expected = reader.GetDecimal(1);
                var actual = reader.GetDecimal(2);
                var movementCount = reader.GetInt32(3);
                if (movementCount != 1 || Diff(expected, actual)) Add("error", "RUN_OUTPUT", id,
                    $"Üretim girişi {actual} ({movementCount} hareket); beklenen {expected}.");
            }

        // Actual raw-material consumption is checked against the BOM for each run.
        await using (var cmd = ProductionDb.Command(conn, tx, """
            SELECT r.UretimId,p.StokKodu,r.Miktar*rec.BirimTuketim,
                   -COALESCE(SUM(CASE WHEN m.HareketTuru='C' THEN m.MiktarDegisimi ELSE 0 END),0)
            FROM dbo.UretimKayitlari r JOIN dbo.UretimEmirleri o ON o.EmirId=r.EmirId
            JOIN dbo.ReceteKalemleri rec ON rec.UrunId=o.UrunId
            JOIN dbo.Urunler p ON p.UrunId=rec.MalzemeId
            LEFT JOIN dbo.StokHareketleri m ON m.UretimId=r.UretimId AND m.UrunId=rec.MalzemeId
            GROUP BY r.UretimId,p.StokKodu,r.Miktar,rec.BirimTuketim;
            """))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(0).ToString();
                var sku = reader.GetString(1);
                var expected = reader.GetDecimal(2);
                var actual = reader.GetDecimal(3);
                if (Diff(expected, actual)) Add("error", "BOM_CONSUMPTION", id,
                    $"Hammadde {sku}: tüketim {actual}, reçete {expected}.");
            }

        // For a reversed run, every product's compensating delta must cancel its
        // original delta; active runs must not contain compensating movements.
        await using (var cmd = ProductionDb.Command(conn, tx, """
            SELECT r.UretimId,p.StokKodu,r.GeriAlmaZamani,
                   SUM(CASE WHEN m.HareketTuru IN ('P','C') THEN m.MiktarDegisimi ELSE 0 END),
                   SUM(CASE WHEN m.HareketTuru='U' THEN m.MiktarDegisimi ELSE 0 END)
            FROM dbo.StokHareketleri m
            JOIN dbo.UretimKayitlari r ON r.UretimId=m.UretimId
            JOIN dbo.Urunler p ON p.UrunId=m.UrunId
            GROUP BY r.UretimId,p.StokKodu,r.GeriAlmaZamani;
            """))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
            {
                var id = reader.GetGuid(0).ToString();
                var sku = reader.GetString(1);
                var reversed = !reader.IsDBNull(2);
                var original = reader.GetDecimal(3);
                var undo = reader.GetDecimal(4);
                if ((reversed && Diff(original + undo, 0)) || (!reversed && Diff(undo, 0)))
                    Add("error", "REVERSAL_LEDGER", id,
                        $"Stok {sku}: orijinal net {original}, ters hareket {undo}.");
            }

        // A low finished-goods balance is a workflow warning, not accounting corruption.
        await using (var cmd = ProductionDb.Command(conn, tx, """
            SELECT r.UretimId,b.MevcutMiktar,r.Miktar FROM dbo.UretimKayitlari r
            JOIN dbo.UretimEmirleri o ON o.EmirId=r.EmirId
            JOIN dbo.StokBakiyeleri b ON b.UrunId=o.UrunId
            WHERE r.GeriAlmaZamani IS NULL AND b.MevcutMiktar < r.Miktar;
            """))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                Add("warning", "UNDO_BLOCKED", reader.GetGuid(0).ToString(),
                    $"Mevcut mamul stoğu {reader.GetDecimal(1)}; geri almak için {reader.GetDecimal(2)} gerekli.");

        // Owned databases record opening stock and corrections separately.
        // The old portfolio demo has no adjustment ledger and retains its narrower checks.
        if(await ProductionDb.ScalarAsync<int>(conn,tx,"SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('dbo') AND name='InventoryAdjustments'",ct)==1)
        {
            await using var cmd=ProductionDb.Command(conn,tx,"""
                SELECT p.StokKodu,b.MevcutMiktar,COALESCE(m.Total,0)+COALESCE(a.Total,0)
                FROM dbo.StokBakiyeleri b JOIN dbo.Urunler p ON p.UrunId=b.UrunId
                LEFT JOIN (SELECT UrunId,SUM(MiktarDegisimi) AS Total FROM dbo.StokHareketleri GROUP BY UrunId) m ON m.UrunId=b.UrunId
                LEFT JOIN (SELECT UrunId,SUM(MiktarDegisimi) AS Total FROM dbo.StokDuzeltmeleri GROUP BY UrunId) a ON a.UrunId=b.UrunId
                WHERE b.MevcutMiktar<>COALESCE(m.Total,0)+COALESCE(a.Total,0);
                """);
            await using var reader=await cmd.ExecuteReaderAsync(ct);
            while(await reader.ReadAsync(ct)) Add("error","STOCK_LEDGER",reader.GetString(0),$"Bakiye {reader.GetDecimal(1)}, hareket toplamı {reader.GetDecimal(2)}.");
        }

        await tx.CommitAsync(ct);
        var sorted = problems.OrderBy(p => p.Severity).ThenBy(p => p.Code).ThenBy(p => p.Reference).ToList();
        return new IntegrityReport(DateTime.UtcNow, orders, runs, sorted,
            sorted.Count(p => p.Severity == "error"), sorted.Count(p => p.Severity == "warning"));
    }
}
