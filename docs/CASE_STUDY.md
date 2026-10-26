# Engineering case study | ProductionFlow Manager

## Problem
Manufacturing operations are vulnerable to deceptively simple inconsistencies: an order's produced quantity might not match posted runs, raw-material usage may drift from the bill of materials, and deleting faulty rows can destroy traceability. An effective workflow needs **pre-save validation, transactional posting, reversible entries and independent integrity diagnostics**.

This repository demonstrates these concerns with an entirely synthetic dataset. It is a software engineering portfolio example, not a claim that the fictional records come from a real factory.

## Scope of the solution

**Write path:**

1. Validate requested quantity and remaining planned amount.
2. Calculate material requirements from product recipes.
3. Check available stock inside the SQL transaction.
4. Record the production run and immutable positive/negative stock movements.
5. Update balances and order progress; add an audit event.
6. Commit on success or roll back all changes on failure.

**Recovery path:**

1. Read original ledger lines and current stock.
2. Deny reversal if already reversed, quantities are inconsistent or finished stock is insufficient.
3. Post exact compensating deltas for each original movement.
4. Update order produced quantity and reversal metadata, record operator/reason and commit.

**Read-only quality path:** Check order/run agreement, lifecycle state, finished receipt quantity, BOM consumption, reversal ledger, and undo feasibility; present grouped error/warning counts and escaped CSV.

## Reproducible demonstration

1. Start with the clean offline demo. The Data Quality tab reports **0 errors / 0 warnings**.
2. Click **Örnek hata oluştur**. A synthetic consumption ledger entry changes by 1 unit.
3. The checker raises `BOM_CONSUMPTION` with actual and expected usage; the report can be exported as CSV.
4. Click **Demoyu sıfırla** and re-run the check. The report returns to zero errors.

![Example detection](integrity-preview.png)

## Key design decisions

| Choice | Why |
|---|---|
| Compensating movements over deletion | Keep the original production history and explain how reversal occurred |
| Serializable transaction & ordered stock locks | Prevent stale inventory decisions and inconsistent partial writes |
| Idempotency request ID | Reject accidental duplicate submissions |
| Dedicated demo SQL database | Eliminate dependence on a real employer's private ERP |
| One offline build and one API-backed UI | Make the project easy to evaluate before setting up SQL Server |
| Read-only diagnostic code path | A report must not silently modify production data |
| Explicit warning vs. error | Low finished stock may block undo without implying corruption |
| Safe, escaped CSV output | Reduce formula injection exposure when opening diagnostic exports in Excel |

## Verification

- 16 Node.js tests passed.
- Browser smoke test: create production → open Data Quality → undo → inject data fault → download CSV. No uncaught browser error.
- SQL Server/.NET build and live integration are **not validated** in the authoring environment, and remain next-stage verification tasks.

## Further work

1. Multi-location inventory and real stock reconciliation with opening/purchase/shipment movement types.
2. Identity provider, operator/supervisor authorization and reversal approval.
3. SQL integration tests in disposable SQL Server containers, including concurrent requests.
4. End-to-end telemetry, alert thresholds and query execution-plan tuning.

## Skills evidenced

T-SQL aggregation and transactional thinking, inventory/accounting invariants, .NET Web API modeling, defensive validation, JavaScript domain logic, test coverage, troubleshooting dashboards, audit-friendly designs, and GitHub CI structure.
