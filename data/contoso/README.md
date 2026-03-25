# ContosoRetailDW Database Backup

This directory should contain the `ContosoRetailDW.bak` file for the SQL Server database.

## Download Instructions

1. **Visit the Microsoft Download Center:**
   https://www.microsoft.com/en-us/download/details.aspx?id=18279

2. **Download the ContosoRetailDW.bak file**
   - File name: `ContosoRetailDW.bak`
   - Size: ~400MB compressed, ~1.6GB when restored

3. **Place the file in this directory:**
   ```
   data/contoso/ContosoRetailDW.bak
   ```

4. **Run the initialization script:**
   ```bash
   ./scripts/init-contoso.sh
   ```

## About ContosoRetailDW

ContosoRetailDW is a sample data warehouse from Microsoft containing:

- **FactSales** - Sales transactions (2007-2009, ~3.4M rows)
- **FactInventory** - Inventory snapshots
- **DimProduct** - Product catalog (~2,500 products)
- **DimCustomer** - Customer information
- **DimStore** - Store locations
- **DimDate** - Date dimension for time analysis
- **DimChannel** - Sales channels
- **DimPromotion** - Marketing promotions

This dataset is ideal for business intelligence queries about retail operations.

## Note

The `.bak` file is excluded from git (see `.gitignore`) due to its large size.
Each developer must download their own copy.
