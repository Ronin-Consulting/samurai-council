# Known Issues

## Issue #1: DimCurrency table not documented in schema

**Date:** 2024-11-28

**Severity:** Low (system recovers gracefully)

**Symptom:**
LLM generates SQL with non-existent column `CurrencySymbol` for `DimCurrency` table:
```
fail: SamurAICouncil.Core.Services.CompanyDataService[0]
      SQL execution error
      Microsoft.Data.SqlClient.SqlException: Invalid column name 'CurrencySymbol'.
```

**Root Cause:**
The `DimCurrency` table is referenced via foreign keys in the schema documentation but the table itself is not documented. The LLM hallucinated a `CurrencySymbol` column based on common naming patterns.

**Actual DimCurrency columns:**
- CurrencyKey (PK)
- CurrencyLabel
- CurrencyName
- CurrencyDescription
- ETLLoadID
- LoadDate
- UpdateDate

**Impact:**
- System recovers gracefully via multi-turn tool retry
- Query eventually succeeds after 1-2 retries
- Slightly increased latency and API costs due to retries

**Workaround:**
None needed - system self-corrects.

**Proposed Fix:**
Add `DimCurrency` table definition to `SamurAICouncil.Core/Resources/ContosoRetailDW_Schema.md`:
```markdown
### DimCurrency
| Column | Type | Description |
|--------|------|-------------|
| CurrencyKey | int | Primary key |
| CurrencyLabel | nvarchar(100) | Currency code label |
| CurrencyName | nvarchar(100) | Full currency name |
| CurrencyDescription | nvarchar(200) | Currency description |
```

**Status:** RESOLVED (Release 7)

**Resolution:**
- Added explicit note in schema: "DimCurrency does NOT have a CurrencySymbol column - use CurrencyLabel for currency codes"
- Added complete DimCurrency column documentation
- Schema now accurately reflects all 4 columns: CurrencyKey, CurrencyLabel, CurrencyName, CurrencyDescription

---

## Issue #2: FactInventory column hallucination

**Date:** 2025-11-28

**Severity:** Medium (query fails)

**Test:** `TestMatrix_Inventory_Turnover`

**Query:** "Inventory turnover by product brand"

**Symptom:**
LLM generates SQL with non-existent columns for FactInventory:
```
Microsoft.Data.SqlClient.SqlException: Invalid column name 'AverageSales'.
Invalid column name 'TotalSold'.
Invalid column name 'UnitsOnHand'.
Invalid column name 'UnitsOrdered'.
```

**Generated SQL:**
```sql
SELECT p.BrandName,
       AVG(i.AverageSales / NULLIF(i.UnitsOnHand, 0)) as InventoryTurnover,
       SUM(i.TotalSold) as TotalUnitsSold,
       AVG(i.UnitsOnHand) as AvgUnitsOnHand
FROM FactInventory i
JOIN DimProduct p ON i.ProductKey = p.ProductKey
GROUP BY p.BrandName
ORDER BY InventoryTurnover DESC;
```

**Actual FactInventory columns:**
- DateKey, ProductKey, StoreKey, CurrencyKey
- OnHandQuantity, OnOrderQuantity, SafetyStockQuantity
- UnitCost, DaysInStock (NOT AverageSales, TotalSold, UnitsOnHand, UnitsOrdered)

**Root Cause:**
Schema documentation for FactInventory may be insufficient or missing column details.

**Status:** RESOLVED (Release 7)

**Resolution:**
- Added explicit warning in schema: "FactInventory uses OnHandQuantity, OnOrderQuantity (NOT UnitsOnHand, UnitsOrdered, TotalSold)"
- Added example query for inventory turnover calculation
- Added warning to CompanyDataService prompt

---

## Issue #3: Missing TOP clause in geographic queries

**Date:** 2025-11-28

**Severity:** Low (query succeeds but returns too many rows)

**Test:** `TestMatrix_Geo_TopCities`

**Query:** "What are the top 10 cities by sales?"

**Symptom:**
Query returns 100 rows instead of 10. The LLM did not include TOP 10 despite the prompt instructing to limit results.

**Generated SQL:**
```sql
SELECT TOP 100 g.CityName, SUM(f.SalesAmount) as TotalSales
FROM FactSales f
JOIN DimStore s ON f.StoreKey = s.StoreKey
JOIN DimGeography g ON s.GeographyKey = g.GeographyKey
GROUP BY g.CityName
ORDER BY TotalSales DESC;
```

**Expected:**
Should use `TOP 10` not `TOP 100` when user asks for "top 10".

**Root Cause:**
The prompt says "Limit results to {MaxRows} rows maximum using TOP" which causes the model to default to TOP 100 (MaxRows) instead of respecting the user's explicit "top 10" request.

**Status:** RESOLVED (Release 7)

**Resolution:**
- Updated prompt rule: "If user asks for 'top N' or 'N best/worst', use TOP N (e.g., 'top 10' = TOP 10)"
- Now the model correctly uses user's specified limit

---

## Issue #4: Low inventory query returns no data

**Date:** 2025-11-28

**Severity:** Low (may not be a bug)

**Test:** `TestMatrix_Inventory_LowStock`

**Query:** "Which products have low inventory?"

**Symptom:**
Query returns 0 rows. This may be correct if no products have low inventory, or the definition of "low" may need clarification.

**Generated SQL:**
```sql
SELECT TOP 100 p.ProductName, i.OnHandQuantity, i.SafetyStockQuantity
FROM FactInventory i
JOIN DimProduct p ON i.ProductKey = p.ProductKey
WHERE i.OnHandQuantity < i.SafetyStockQuantity
ORDER BY (i.SafetyStockQuantity - i.OnHandQuantity) DESC;
```

**Note:**
The SQL logic is correct (OnHandQuantity < SafetyStockQuantity = low stock). The 0 rows may simply mean no products are below safety stock level in the dataset.

**Status:** NOT A BUG (data-dependent)

---

## Issue #5: Sales by country query fails intermittently

**Date:** 2025-11-28

**Severity:** Medium (query fails)

**Test:** `TestMatrix_Geo_SalesByCountry`

**Query:** "Total sales by country"

**Symptom:**
Query fails with "Invalid column name 'CountryRegionName'" error.

**Root Cause:**
The schema documentation incorrectly listed `CountryRegionName` as the country column. The actual column name in ContosoRetailDW is `RegionCountryName`.

**DimGeography Country Columns:**
- `RegionCountryName` - **Correct column for country**
- Do NOT use: CountryRegionName, Country, CountryName (these don't exist)

**Status:** RESOLVED (Release 8)

**Resolution:**
- Fixed schema documentation to use correct column name `RegionCountryName`
- Updated CompanyDataService prompt to warn about incorrect column names
- Updated example queries in schema to use correct column

**Reference:** [Microsoft SQL Server samples](https://github.com/microsoft/sql-server-samples/blob/master/samples/databases/contoso-data-warehouse/load-contoso-data-warehouse-to-sql-data-warehouse.sql)

---
