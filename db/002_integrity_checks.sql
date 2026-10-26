-- ProductionFlowDemo: READ-ONLY checks. No UPDATE, DELETE, or schema mutations.
-- Run only after db/001_create_and_seed.sql on the synthetic DEMO database.
USE ProductionFlowDemo;
GO

-- 1: Order amounts and status must match active production runs.
SELECT o.OrderId,o.Code,o.Planned,o.Produced,o.Status,
       COALESCE(SUM(CASE WHEN r.ReversedAt IS NULL THEN r.Quantity ELSE 0 END),0) AS ActiveRunsTotal,
       CASE WHEN o.Produced <> COALESCE(SUM(CASE WHEN r.ReversedAt IS NULL THEN r.Quantity ELSE 0 END),0)
                THEN 'ORDER_QUANTITY'
            WHEN o.Status <> CASE WHEN o.Produced <= 0 THEN N'Başladı'
                                 WHEN o.Produced >= o.Planned THEN N'Kapandı'
                                 ELSE N'Üretiliyor' END THEN 'ORDER_STATUS'
            ELSE 'OK' END AS CheckResult
FROM dbo.Orders o LEFT JOIN dbo.ProductionRuns r ON r.OrderId=o.OrderId
GROUP BY o.OrderId,o.Code,o.Planned,o.Produced,o.Status
ORDER BY o.OrderId;
GO

-- 2: Output movements must match the quantity of the run.
SELECT r.RunId,o.Code,r.Quantity,
       COALESCE(SUM(CASE WHEN m.MovementType='P' AND m.ProductId=o.ProductId THEN m.Delta ELSE 0 END),0) AS RecordedOutput,
       SUM(CASE WHEN m.MovementType='P' AND m.ProductId=o.ProductId THEN 1 ELSE 0 END) AS OutputMovementCount,
       CASE WHEN r.Quantity <> COALESCE(SUM(CASE WHEN m.MovementType='P' AND m.ProductId=o.ProductId THEN m.Delta ELSE 0 END),0)
                  OR SUM(CASE WHEN m.MovementType='P' AND m.ProductId=o.ProductId THEN 1 ELSE 0 END)<>1
             THEN 'RUN_OUTPUT' ELSE 'OK' END AS CheckResult
FROM dbo.ProductionRuns r JOIN dbo.Orders o ON o.OrderId=r.OrderId
LEFT JOIN dbo.StockMovements m ON m.RunId=r.RunId
GROUP BY r.RunId,o.Code,r.Quantity,o.ProductId;
GO

-- 3: Raw material use must match the recipe for each run.
SELECT r.RunId,o.Code,p.Sku, r.Quantity*rec.PerUnit AS RecipeConsumption,
       -COALESCE(SUM(CASE WHEN m.MovementType='C' THEN m.Delta ELSE 0 END),0) AS RecordedConsumption,
       CASE WHEN r.Quantity*rec.PerUnit <> -COALESCE(SUM(CASE WHEN m.MovementType='C' THEN m.Delta ELSE 0 END),0)
             THEN 'BOM_CONSUMPTION' ELSE 'OK' END AS CheckResult
FROM dbo.ProductionRuns r JOIN dbo.Orders o ON o.OrderId=r.OrderId
JOIN dbo.Recipes rec ON rec.ProductId=o.ProductId
JOIN dbo.Products p ON p.ProductId=rec.MaterialId
LEFT JOIN dbo.StockMovements m ON m.RunId=r.RunId AND m.ProductId=rec.MaterialId
GROUP BY r.RunId,o.Code,p.Sku,r.Quantity,rec.PerUnit;
GO

-- 4: Undo ledger must cancel originals exactly, grouped per SKU.
SELECT r.RunId,p.Sku,r.ReversedAt,
       SUM(CASE WHEN m.MovementType IN ('P','C') THEN m.Delta ELSE 0 END) AS OriginalDelta,
       SUM(CASE WHEN m.MovementType='U' THEN m.Delta ELSE 0 END) AS ReversalDelta,
       CASE WHEN r.ReversedAt IS NOT NULL AND SUM(m.Delta)<>0 THEN 'REVERSAL_LEDGER'
            WHEN r.ReversedAt IS NULL AND SUM(CASE WHEN m.MovementType='U' THEN m.Delta ELSE 0 END)<>0 THEN 'REVERSAL_LEDGER'
            ELSE 'OK' END AS CheckResult
FROM dbo.StockMovements m JOIN dbo.ProductionRuns r ON r.RunId=m.RunId
JOIN dbo.Products p ON p.ProductId=m.ProductId
GROUP BY r.RunId,p.Sku,r.ReversedAt;
GO

-- 5: Operational warnings: undo is unavailable for a run if FG was consumed.
SELECT r.RunId,o.Code,b.OnHand AS FinishedOnHand,r.Quantity AS UndoNeeds,'UNDO_BLOCKED' AS Warning
FROM dbo.ProductionRuns r JOIN dbo.Orders o ON o.OrderId=r.OrderId
JOIN dbo.StockBalances b ON b.ProductId=o.ProductId
WHERE r.ReversedAt IS NULL AND b.OnHand < r.Quantity;
GO
