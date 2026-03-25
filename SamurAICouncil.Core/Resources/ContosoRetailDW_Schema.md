# ContosoRetailDW Database Schema

This document describes the schema of the ContosoRetailDW data warehouse, which contains retail sales data from 2007-2009 for a fictional electronics retailer.

## Overview

The database follows a star schema design with:
- **Fact Tables**: Transaction and event data (sales, inventory, IT operations)
- **Dimension Tables**: Descriptive attributes (products, customers, stores, dates)
- **Views**: Pre-built views for analytics and data mining

---

## Fact Tables

### FactSales
**Summary table containing per store/per product daily sales data (~3.4 million rows, 2007-2009)**

| Column | Type | Description |
|--------|------|-------------|
| SalesKey | int | Primary key |
| DateKey | datetime | FK to DimDate - transaction date |
| ChannelKey | int | FK to DimChannel - sales channel |
| StoreKey | int | FK to DimStore - store location |
| ProductKey | int | FK to DimProduct - product sold |
| PromotionKey | int | FK to DimPromotion - promotion applied |
| CurrencyKey | int | FK to DimCurrency |
| UnitCost | money | Cost per unit |
| UnitPrice | money | Price per unit |
| SalesQuantity | int | Number of units sold |
| ReturnQuantity | int | Number of units returned |
| ReturnAmount | money | Total return amount |
| DiscountQuantity | int | Quantity discounted |
| DiscountAmount | money | Discount amount |
| TotalCost | money | Total cost |
| SalesAmount | money | Total sales amount |

**Key metrics:** SalesAmount, TotalCost, SalesQuantity, ReturnAmount, DiscountAmount

### FactOnlineSales
**Transactional data for individual online sales with order details**

| Column | Type | Description |
|--------|------|-------------|
| OnlineSalesKey | int | Primary key |
| DateKey | datetime | FK to DimDate |
| StoreKey | int | FK to DimStore |
| ProductKey | int | FK to DimProduct |
| PromotionKey | int | FK to DimPromotion |
| CurrencyKey | int | FK to DimCurrency |
| CustomerKey | int | FK to DimCustomer |
| SalesOrderNumber | nvarchar(20) | Purchase order number |
| SalesOrderLineNumber | int | Line item of a specific order |
| SalesQuantity | int | Sale quantity |
| SalesAmount | money | Sales amount |
| ReturnQuantity | int | Quantity of returned goods |
| ReturnAmount | money | Amount of returned goods |
| DiscountQuantity | int | Discount quantity |
| DiscountAmount | money | Discount amount |
| TotalCost | money | Total cost |
| UnitCost | money | Unit cost |
| UnitPrice | money | Unit price |

### FactInventory
**Weekly inventory snapshot per store/per product**

⚠️ **IMPORTANT**: Use ONLY these exact column names. Do NOT use: UnitsOnHand, UnitsOrdered, TotalSold, AverageSales - these do NOT exist!

| Column | Type | Description |
|--------|------|-------------|
| InventoryKey | int | Primary key |
| DateKey | datetime | FK to DimDate |
| StoreKey | int | FK to DimStore |
| ProductKey | int | FK to DimProduct |
| CurrencyKey | int | FK to DimCurrency |
| OnHandQuantity | int | Available quantity (NOT "UnitsOnHand") |
| OnOrderQuantity | int | Ordered quantity (NOT "UnitsOrdered") |
| SafetyStockQuantity | int | Quantity of safety stock |
| UnitCost | money | Average unit cost of product |
| DaysInStock | int | Days products stayed in stock |
| MinDayInStock | int | Minimum days in stock |
| MaxDayInStock | int | Maximum days in stock |
| Aging | int | Days goods in stock can meet sales needs |

**Inventory Turnover Example:**
```sql
-- Inventory turnover by brand (correct columns!)
SELECT p.BrandName,
       AVG(i.OnHandQuantity) as AvgOnHand,
       AVG(i.DaysInStock) as AvgDaysInStock
FROM FactInventory i
JOIN DimProduct p ON i.ProductKey = p.ProductKey
GROUP BY p.BrandName;
```

### FactExchangeRate
**Daily exchange rates converted to base currency (USD)**

| Column | Type | Description |
|--------|------|-------------|
| ExchangeRateKey | int | Primary key |
| CurrencyKey | int | FK to DimCurrency |
| DateKey | datetime | FK to DimDate |
| AverageRate | float | Average rate of the day |
| EndOfDayRate | float | Rate at end of day |

### FactSalesQuota
**Sales operational plan with actual, forecast, and budget data**

| Column | Type | Description |
|--------|------|-------------|
| SalesQuotaKey | int | Primary key |
| StoreKey | int | FK to DimStore |
| ProductKey | int | FK to DimProduct |
| ChannelKey | int | FK to DimChannel |
| DateKey | datetime | FK to DimDate |
| CurrencyKey | int | FK to DimCurrency |
| ScenarioKey | int | FK to DimScenario (Actual/Budget/Forecast) |
| SalesAmountQuota | money | Sales amount target |
| SalesQuantityQuota | money | Sales quantity target |
| GrossMarginQuota | money | Gross margin target |

### FactStrategyPlan
**Corporate strategic plan with P&L data**

| Column | Type | Description |
|--------|------|-------------|
| StrategyPlanKey | int | Primary key |
| Datekey | datetime | FK to DimDate |
| EntityKey | int | FK to DimEntity (Group/Country/Region/Store) |
| ScenarioKey | int | FK to DimScenario |
| AccountKey | int | FK to DimAccount (P&L accounts) |
| CurrencyKey | int | FK to DimCurrency |
| ProductCategoryKey | int | FK to DimProductCategory |
| Amount | money | Actual/budget/forecast amount |

### FactITMachine
**Machine procurement and maintenance costs**

| Column | Type | Description |
|--------|------|-------------|
| ITMachinekey | int | Primary key |
| MachineKey | int | FK to DimMachine |
| Datekey | datetime | FK to DimDate |
| CostAmount | money | Actual cost of machine |
| CostType | nvarchar(200) | Cost type (Maintenance/Purchase/...) |

### FactITSLA
**IT outage and service level information**

| Column | Type | Description |
|--------|------|-------------|
| ITSLAkey | int | Primary key |
| DateKey | datetime | FK to DimDate |
| StoreKey | int | FK to DimStore |
| MachineKey | int | FK to DimMachine |
| OutageKey | int | FK to DimOutage |
| OutageStartTime | datetime | When outage started |
| OutageEndTime | datetime | When outage resolved |
| DownTime | int | Machine down time (minutes) |

---

## Dimension Tables

### DimProduct
**Product catalog (~2,500 products)**

| Column | Type | Description |
|--------|------|-------------|
| ProductKey | int | Primary key |
| ProductLabel | nvarchar(100) | Product SKU/code |
| ProductName | nvarchar(500) | Product name |
| ProductDescription | nvarchar(400) | Full description |
| ProductSubcategoryKey | int | FK to DimProductSubcategory |
| Manufacturer | nvarchar(50) | Manufacturer name |
| BrandName | nvarchar(50) | Brand name |
| ClassName | nvarchar(20) | Product class (Economy, Regular, Deluxe) |
| StyleName | nvarchar(20) | Style category |
| ColorName | nvarchar(20) | Product color |
| Size | nvarchar(50) | Size designation |
| SizeRange | nvarchar(50) | Size range category |
| Weight | float | Product weight |
| UnitCost | money | Standard cost |
| UnitPrice | money | List price |
| AvailableForSaleDate | datetime | Date available |
| StopSaleDate | datetime | Discontinued date |
| Status | nvarchar(7) | Current/Discontinued |

**Product Hierarchy:** ProductCategory → ProductSubcategory → Product

**Categories:** Audio, Cameras and Camcorders, Cell phones, Computers, Games and Toys, Home Appliances, Music/Movies/Audio Books, TV and Video

### DimProductSubcategory

| Column | Type | Description |
|--------|------|-------------|
| ProductSubcategoryKey | int | Primary key |
| ProductSubcategoryLabel | nvarchar(100) | Subcategory code |
| ProductSubcategoryName | nvarchar(50) | Subcategory name |
| ProductCategoryKey | int | FK to DimProductCategory |

### DimProductCategory

| Column | Type | Description |
|--------|------|-------------|
| ProductCategoryKey | int | Primary key |
| ProductCategoryLabel | nvarchar(100) | Category code |
| ProductCategoryName | nvarchar(30) | Category name |

### DimCustomer
**Customer information**

| Column | Type | Description |
|--------|------|-------------|
| CustomerKey | int | Primary key |
| GeographyKey | int | FK to DimGeography |
| CustomerLabel | nvarchar(100) | Customer ID |
| Title | nvarchar(8) | Mr/Mrs/Ms |
| FirstName | nvarchar(50) | First name |
| MiddleName | nvarchar(50) | Middle name |
| LastName | nvarchar(50) | Last name |
| BirthDate | date | Date of birth |
| MaritalStatus | nvarchar(1) | M=Married, S=Single |
| Gender | nvarchar(1) | M=Male, F=Female |
| EmailAddress | nvarchar(50) | Email address |
| YearlyIncome | money | Annual income |
| TotalChildren | tinyint | Total number of children |
| NumberChildrenAtHome | tinyint | Children living at home |
| Education | nvarchar(40) | Education level (Bachelors, Graduate Degree, High School, Partial College, Partial High School) |
| Occupation | nvarchar(100) | Job type (Clerical, Management, Manual, Professional, Skilled Manual) |
| HouseOwnerFlag | nchar(1) | Y/N owns house |
| NumberCarsOwned | tinyint | Cars owned |
| AddressLine1 | nvarchar(120) | Street address |
| AddressLine2 | nvarchar(120) | Address line 2 |
| Phone | nvarchar(20) | Phone number |
| DateFirstPurchase | date | First purchase date |
| CustomerType | nvarchar(15) | Person/Company |

### DimStore
**Store locations**

| Column | Type | Description |
|--------|------|-------------|
| StoreKey | int | Primary key |
| GeographyKey | int | FK to DimGeography |
| StoreManager | int | Manager employee key |
| StoreType | nvarchar(15) | Store/Catalog/Online |
| StoreName | nvarchar(100) | Store name |
| StoreDescription | nvarchar(300) | Description |
| Status | nvarchar(20) | On/Off status |
| OpenDate | datetime | Opening date |
| CloseDate | datetime | Closing date |
| EntityKey | int | FK to DimEntity |
| ZipCode | nvarchar(20) | ZIP code |
| StorePhone | nvarchar(15) | Phone number |
| StoreFax | nvarchar(14) | Fax number |
| AddressLine1 | nvarchar(100) | Address |
| AddressLine2 | nvarchar(100) | Address line 2 |
| CloseReason | nvarchar(20) | Reason for closure |
| EmployeeCount | int | Number of employees |
| SellingAreaSize | float | Square footage |
| LastRemodelDate | datetime | Last remodel date |

**Store Hierarchy:** Region/Country → State → City → Store

### DimDate
**Date dimension for time analysis**

⚠️ **CRITICAL**: CalendarQuarter and CalendarMonth use YYYYQ/YYYYMM format, NOT simple numbers!

| Column | Type | Description |
|--------|------|-------------|
| Datekey | datetime | Primary key |
| FullDateLabel | nvarchar(20) | Date label |
| DateDescription | nvarchar(20) | Date description |
| CalendarYear | int | Year (2007, 2008, 2009) |
| CalendarYearLabel | nvarchar(20) | "CY 2007" format |
| CalendarHalfYear | int | 1 or 2 |
| CalendarHalfYearLabel | nvarchar(20) | Half year label |
| CalendarQuarter | int | **YYYYQ format**: 20071, 20072, 20081, 20082, etc. Use CalendarQuarterLabel for Q1-Q4 |
| CalendarQuarterLabel | nvarchar(20) | "Q1", "Q2", "Q3", "Q4" - use this for quarter filtering! |
| CalendarMonth | int | **YYYYMM format**: 200701, 200702, 200801, etc. NOT 1-12! |
| CalendarMonthLabel | nvarchar(20) | "January", "February", etc. |
| CalendarWeek | int | Week number |
| CalendarWeekLabel | nvarchar(20) | Week label |
| CalendarDayOfWeek | int | Day number (1=Sunday) |
| CalendarDayOfWeekLabel | nvarchar(10) | "Monday", etc. |
| FiscalYear | int | Fiscal year |
| FiscalYearLabel | nvarchar(20) | Fiscal year label |
| FiscalHalfYear | int | Fiscal half year |
| FiscalQuarter | int | Fiscal quarter |
| FiscalQuarterLabel | nvarchar(20) | Fiscal quarter label |
| FiscalMonth | int | Fiscal month |
| FiscalMonthLabel | nvarchar(20) | Fiscal month label |
| IsWorkDay | nvarchar(20) | Whether it is a work day |
| IsHoliday | int | 1=Holiday |
| HolidayName | nvarchar(20) | Holiday name if applicable |
| EuropeSeason | nvarchar(50) | Marketing season in Europe |
| NorthAmericaSeason | nvarchar(50) | Marketing season in North America |
| AsiaSeason | nvarchar(50) | Marketing season in Asia |

**Calendar Hierarchies:**
- Calendar YQMD: Year → Quarter → Month → Day
- Calendar YWD: Year → Week → Weekday → Day
- Fiscal YQM: Fiscal Year → Half Year → Quarter → Month

### DimChannel
**Sales channels**

| Column | Type | Description |
|--------|------|-------------|
| ChannelKey | int | Primary key |
| ChannelLabel | nvarchar(100) | Channel code |
| ChannelName | nvarchar(20) | Store/Online/Catalog/Reseller |
| ChannelDescription | nvarchar(50) | Description |

### DimPromotion
**Marketing promotions**

| Column | Type | Description |
|--------|------|-------------|
| PromotionKey | int | Primary key |
| PromotionLabel | nvarchar(100) | Promotion code |
| PromotionName | nvarchar(100) | Promotion name |
| PromotionDescription | nvarchar(255) | Description |
| DiscountPercent | float | Discount percentage |
| PromotionType | nvarchar(50) | Type (No Discount, Excess Inventory, Seasonal Discount, etc.) |
| PromotionCategory | nvarchar(50) | Category |
| StartDate | datetime | Start date |
| EndDate | datetime | End date |
| MinQuantity | int | Minimum quantity |
| MaxQuantity | int | Maximum quantity |

### DimCurrency
**Currency information**

| Column | Type | Description |
|--------|------|-------------|
| CurrencyKey | int | Primary key |
| CurrencyLabel | nvarchar(10) | Currency abbreviation (USD, EUR, GBP, CNY, etc.) |
| CurrencyName | nvarchar(20) | Currency name |
| CurrencyDescription | nvarchar(50) | Description |

### DimGeography
**Geographic regions**

⚠️ **CRITICAL**: The country column is `RegionCountryName` (NOT "CountryRegionName", "Country", or "CountryName")

| Column | Type | Description |
|--------|------|-------------|
| GeographyKey | int | Primary key |
| GeographyType | nvarchar(50) | Type (Continent/RegionCountry/StateProvince/City) |
| ContinentName | nvarchar(50) | Continent (Asia/Europe/North America) |
| RegionCountryName | nvarchar(100) | **Country name - USE THIS for country queries!** |
| StateProvinceName | nvarchar(100) | State/Province |
| CityName | nvarchar(100) | City |

**Geography Hierarchy:** Continent → Region/Country → State/Province → City

**Sales by Country Example:**
```sql
SELECT g.RegionCountryName, SUM(f.SalesAmount) as TotalSales
FROM FactSales f
JOIN DimStore s ON f.StoreKey = s.StoreKey
JOIN DimGeography g ON s.GeographyKey = g.GeographyKey
GROUP BY g.RegionCountryName
ORDER BY TotalSales DESC;
```

### DimSalesTerritory
**Sales territory mapping for regional analysis**

| Column | Type | Description |
|--------|------|-------------|
| SalesTerritoryKey | int | Primary key |
| GeographyKey | int | FK to DimGeography |
| SalesTerritoryLabel | nvarchar(100) | Territory code |
| SalesTerritoryName | nvarchar(50) | Territory name (Contoso Redmond Store, etc.) |
| SalesTerritoryRegion | nvarchar(50) | Region (Colorado, Wisconsin, Texas, Florida) |
| SalesTerritoryCountry | nvarchar(50) | Country (United States, UK, Switzerland) |
| SalesTerritoryGroup | nvarchar(50) | Group (North American, Asian, European) |
| SalesTerritoryLevel | nvarchar(10) | Territory level |
| SalesTerritoryManager | int | Manager employee key |
| StartDate | datetime | Start date (SCD) |
| EndDate | datetime | End date (SCD) |
| Status | nvarchar(50) | Current/Retired |

**Territory Hierarchy:** Group → Country → Region → Territory

### DimEmployee
**Employee information**

| Column | Type | Description |
|--------|------|-------------|
| EmployeeKey | int | Primary key |
| ParentEmployeeKey | int | Direct report manager key |
| FirstName | nvarchar(50) | First name |
| LastName | nvarchar(50) | Last name |
| MiddleName | nvarchar(50) | Middle name |
| Title | nvarchar(50) | Job title |
| HireDate | date | Hire date |
| BirthDate | date | Birth date |
| EmailAddress | nvarchar(50) | Email |
| Phone | nvarchar(25) | Phone number |
| MaritalStatus | nchar(1) | Marital status |
| Gender | nchar(1) | Gender |
| BaseRate | money | Base salary rate |
| VacationHours | smallint | Vacation hours |
| DepartmentName | nvarchar(50) | Department |
| StartDate | date | Start date |
| EndDate | date | End date |
| Status | nvarchar(50) | Status |
| SalesPersonFlag | bit | Is salesperson |
| CurrentFlag | bit | Is current employee |

**Employee Hierarchy:** Parent-child relationship via ParentEmployeeKey

### DimEntity
**Business entities (corporate structure)**

| Column | Type | Description |
|--------|------|-------------|
| EntityKey | int | Primary key |
| EntityLabel | nvarchar(100) | Entity label |
| ParentEntityKey | int | Parent entity key |
| ParentEntityLabel | nvarchar(100) | Parent entity label |
| EntityName | nvarchar(50) | Entity name |
| EntityDescription | nvarchar(100) | Description |
| EntityType | nvarchar(100) | Type (Group/Country/Region/Store) |
| StartDate | datetime | Start date (for SCD) |
| EndDate | datetime | End date (for SCD) |
| Status | nvarchar(50) | Current/Retired |

**Entity Hierarchy:** Parent-child: Group → Region/Country → State → Store

### DimAccount
**Financial accounts (P&L structure)**

| Column | Type | Description |
|--------|------|-------------|
| AccountKey | int | Primary key |
| ParentAccountKey | int | Parent account key |
| AccountLabel | nvarchar(100) | Account label |
| AccountName | nvarchar(50) | Account name |
| AccountDescription | nvarchar(50) | Description |
| AccountType | nvarchar(50) | Type (Asset, Income, Expense, etc.) |
| Operator | nvarchar(50) | Calculation operator |
| ValueType | nvarchar(50) | Value type |

**Account Hierarchy:** Parent-child for P&L accounts

### DimScenario
**Planning scenarios**

| Column | Type | Description |
|--------|------|-------------|
| ScenarioKey | int | Primary key |
| ScenarioLabel | nvarchar(100) | Scenario code |
| ScenarioName | nvarchar(20) | Actual/Budget/Forecast |
| ScenarioDescription | nvarchar(50) | Description |

### DimMachine
**POS machines, servers, and IT equipment**

| Column | Type | Description |
|--------|------|-------------|
| MachineKey | int | Primary key |
| MachineLabel | nvarchar(100) | Machine label (POS0128201, etc.) |
| StoreKey | int | FK to DimStore |
| MachineType | nvarchar(50) | Machine type (POS generation, Server type) |
| MachineName | nvarchar(100) | Machine name |
| MachineDescription | nvarchar(200) | Description |
| VendorName | nvarchar(50) | Procurement vendor |
| MachineOS | nvarchar(50) | Operating system |
| MachineSource | nvarchar(100) | Location (Store/Data Center) |
| MachineHardware | nvarchar(100) | Hardware configuration |
| MachineSoftware | nvarchar(100) | Software configuration |
| Status | nvarchar(50) | Status (Active/Decommission) |
| ServiceStartDate | datetime | Service start date |
| DecommissionDate | datetime | Decommission date |
| LastModifiedDate | datetime | Last updated date |

### DimOutage
**IT outage types**

| Column | Type | Description |
|--------|------|-------------|
| OutageKey | int | Primary key |
| OutageLabel | nvarchar(100) | Outage code |
| OutageName | nvarchar(50) | Outage name |
| OutageDescription | nvarchar(200) | Description |
| OutageType | nvarchar(50) | Outage type |
| OutageTypeDescription | nvarchar(200) | Type description |
| OutageSubType | nvarchar(50) | Outage sub-type |
| OutageSubTypeDescription | nvarchar(200) | Sub-type description |

**Outage Hierarchy:** OutageType → OutageSubType → OutageName

---

## Views (Data Mining)

### V_Customer
**Customer classification view for data mining**

| Column | Type | Description |
|--------|------|-------------|
| CustomerKey | int | Customer key |
| Age | int | Customer age |
| MaritalStatus | nchar(1) | Marital status |
| Gender | nvarchar(1) | Gender |
| YearlyIncome | money | Yearly income |
| TotalChildren | tinyint | Number of children |
| NumberChildrenAtHome | tinyint | Children at home |
| Education | nvarchar(40) | Education level |
| HouseOwnerFlag | nchar(1) | Owns house |
| NumberCarsOwned | tinyint | Cars owned |
| Consumption | money | Total consumption amount |

### V_CustomerPromotion
**Customer promotion analysis view**

| Column | Type | Description |
|--------|------|-------------|
| CustomerKey | int | Customer key |
| PromotionKey | int | Promotion key |
| PromotionName | nvarchar(20) | Promotion name |
| PromotionType | nvarchar(50) | Promotion type |
| ProductKey | int | Product key |
| MaritalStatus | nvarchar(1) | Marital status |
| Gender | nvarchar(1) | Gender |
| YearlyIncome | money | Yearly income |
| TotalChildren | tinyint | Number of children |
| NumberChildrenAtHome | tinyint | Children at home |
| Education | nvarchar(40) | Education level |
| HouseOwnerFlag | nchar(1) | Owns house (Y/N) |
| NumberCarsOwned | tinyint | Cars owned |
| Age | int | Customer age |

### V_CustomerOrders
**Customer order summary view for basket analysis**

| Column | Type | Description |
|--------|------|-------------|
| OrderNumber | nvarchar(20) | Sales order number |
| LineNumber | int | Line item of order |
| CalendarYear | int | Calendar year |
| FiscalYear | int | Fiscal year |
| Month | int | Calendar month |
| ProductCategoryName | nvarchar(30) | Category name |
| ProductSubcategory | nvarchar(50) | Subcategory name |
| Product | nvarchar(500) | Product name |
| CustomerKey | int | Customer key |
| Region | nvarchar(100) | Country/Region name |
| IncomeGroup | nvarchar(8) | Income group (Low/High/Moderate) |
| Age | int | Customer age at purchase |
| Quantity | int | Sales quantity |
| Amount | money | Sales amount |

### V_OnlineSalesOrder
**Online sales order header view for basket analysis**

| Column | Type | Description |
|--------|------|-------------|
| OrderNumber | nvarchar(20) | Sales order number |
| CustomerKey | int | Customer key |
| Region | nvarchar(100) | Country/Region name |
| IncomeGroup | nvarchar(8) | Income level (Low/High/Moderate) |

### V_OnlineSalesOrderDetail
**Online sales order detail view for basket analysis**

| Column | Type | Description |
|--------|------|-------------|
| OrderNumber | nvarchar(20) | Sales order number |
| LineNumber | int | Order line number |
| Product | nvarchar(500) | Product name |

### V_ProductForecast
**Product forecast view for planning and data mining**

| Column | Type | Description |
|--------|------|-------------|
| CalendarMonth | int | Calendar month |
| ReportDate | date | First day of month |
| ProductCategoryName | nvarchar(30) | Category name |
| SalesQuantity | int | Sales quantity |
| SalesAmount | money | Sales amount |

---

## Common Query Patterns

### Sales Analysis
```sql
-- Total sales by year
SELECT d.CalendarYear, SUM(f.SalesAmount) as TotalSales
FROM FactSales f
JOIN DimDate d ON f.DateKey = d.Datekey
GROUP BY d.CalendarYear
ORDER BY d.CalendarYear;

-- ⚠️ IMPORTANT: Quarterly filtering - use CalendarQuarterLabel, NOT CalendarQuarter!
-- Q1 2008 profit (correct way)
SELECT SUM(f.SalesAmount) - SUM(f.TotalCost) - SUM(f.ReturnAmount) as Profit
FROM FactSales f
JOIN DimDate d ON f.DateKey = d.Datekey
WHERE d.CalendarYear = 2008 AND d.CalendarQuarterLabel = 'Q1';

-- Alternative: Filter by month range
SELECT SUM(f.SalesAmount) - SUM(f.TotalCost) - SUM(f.ReturnAmount) as Profit
FROM FactSales f
JOIN DimDate d ON f.DateKey = d.Datekey
WHERE d.CalendarYear = 2008 AND MONTH(d.Datekey) IN (1, 2, 3);

-- Top 10 products by revenue
SELECT TOP 10 p.ProductName, SUM(f.SalesAmount) as Revenue
FROM FactSales f
JOIN DimProduct p ON f.ProductKey = p.ProductKey
GROUP BY p.ProductName
ORDER BY Revenue DESC;

-- Sales by store
SELECT s.StoreName, SUM(f.SalesAmount) as TotalSales
FROM FactSales f
JOIN DimStore s ON f.StoreKey = s.StoreKey
GROUP BY s.StoreName
ORDER BY TotalSales DESC;

-- Sales by channel
SELECT c.ChannelName, SUM(f.SalesAmount) as TotalSales
FROM FactSales f
JOIN DimChannel c ON f.ChannelKey = c.ChannelKey
GROUP BY c.ChannelName;

-- Monthly sales trend
SELECT d.CalendarYear, d.CalendarMonth, d.CalendarMonthLabel,
       SUM(f.SalesAmount) as TotalSales
FROM FactSales f
JOIN DimDate d ON f.DateKey = d.Datekey
GROUP BY d.CalendarYear, d.CalendarMonth, d.CalendarMonthLabel
ORDER BY d.CalendarYear, d.CalendarMonth;

-- Gross margin analysis
SELECT d.CalendarYear,
       SUM(f.SalesAmount) as Revenue,
       SUM(f.TotalCost) as Cost,
       SUM(f.SalesAmount) - SUM(f.TotalCost) - SUM(f.ReturnAmount) as GrossMargin
FROM FactSales f
JOIN DimDate d ON f.DateKey = d.Datekey
GROUP BY d.CalendarYear;
```

### Product Analysis
```sql
-- Sales by product category
SELECT pc.ProductCategoryName, SUM(f.SalesAmount) as TotalSales
FROM FactSales f
JOIN DimProduct p ON f.ProductKey = p.ProductKey
JOIN DimProductSubcategory ps ON p.ProductSubcategoryKey = ps.ProductSubcategoryKey
JOIN DimProductCategory pc ON ps.ProductCategoryKey = pc.ProductCategoryKey
GROUP BY pc.ProductCategoryName
ORDER BY TotalSales DESC;

-- Sales by brand
SELECT p.BrandName, SUM(f.SalesAmount) as TotalSales, SUM(f.SalesQuantity) as UnitsSold
FROM FactSales f
JOIN DimProduct p ON f.ProductKey = p.ProductKey
GROUP BY p.BrandName
ORDER BY TotalSales DESC;
```

### Customer Analysis
```sql
-- Sales by customer demographics
SELECT c.Gender, c.MaritalStatus, c.Education,
       COUNT(DISTINCT c.CustomerKey) as Customers,
       SUM(f.SalesAmount) as TotalSales
FROM FactOnlineSales f
JOIN DimCustomer c ON f.CustomerKey = c.CustomerKey
GROUP BY c.Gender, c.MaritalStatus, c.Education;

-- Top customers by purchase amount
SELECT TOP 10 c.FirstName, c.LastName, SUM(f.SalesAmount) as TotalPurchases
FROM FactOnlineSales f
JOIN DimCustomer c ON f.CustomerKey = c.CustomerKey
GROUP BY c.CustomerKey, c.FirstName, c.LastName
ORDER BY TotalPurchases DESC;
```

### Geographic Analysis
```sql
-- Sales by country (use RegionCountryName, NOT CountryRegionName!)
SELECT g.RegionCountryName, SUM(f.SalesAmount) as TotalSales
FROM FactSales f
JOIN DimStore s ON f.StoreKey = s.StoreKey
JOIN DimGeography g ON s.GeographyKey = g.GeographyKey
GROUP BY g.RegionCountryName
ORDER BY TotalSales DESC;
```

### Inventory Analysis
```sql
-- Current inventory levels by product
SELECT p.ProductName, SUM(i.OnHandQuantity) as OnHand, SUM(i.OnOrderQuantity) as OnOrder
FROM FactInventory i
JOIN DimProduct p ON i.ProductKey = p.ProductKey
JOIN DimDate d ON i.DateKey = d.Datekey
WHERE d.Datekey = (SELECT MAX(Datekey) FROM DimDate)
GROUP BY p.ProductName
ORDER BY OnHand DESC;
```

---

## Key Relationships

| Fact Table | Related Dimensions |
|------------|-------------------|
| FactSales | DimDate, DimChannel, DimStore, DimProduct, DimPromotion, DimCurrency |
| FactOnlineSales | DimDate, DimStore, DimProduct, DimPromotion, DimCurrency, DimCustomer |
| FactInventory | DimDate, DimStore, DimProduct, DimCurrency |
| FactExchangeRate | DimDate, DimCurrency |
| FactSalesQuota | DimDate, DimStore, DimProduct, DimChannel, DimCurrency, DimScenario |
| FactStrategyPlan | DimDate, DimEntity, DimScenario, DimAccount, DimCurrency, DimProductCategory |
| FactITMachine | DimDate, DimMachine |
| FactITSLA | DimDate, DimStore, DimMachine, DimOutage |

| Dimension Table | Related Dimensions |
|-----------------|-------------------|
| DimStore | DimGeography, DimEntity |
| DimCustomer | DimGeography |
| DimProduct | DimProductSubcategory |
| DimProductSubcategory | DimProductCategory |
| DimEmployee | DimEmployee (ParentEmployeeKey - self-referential) |
| DimSalesTerritory | DimGeography |
| DimMachine | DimStore |
| DimAccount | DimAccount (ParentAccountKey - self-referential) |
| DimEntity | DimEntity (ParentEntityKey - self-referential) |

---

## Notes

### General
- All monetary values are in the currency specified by CurrencyKey (primarily USD)
- **Date range:** January 1, 2007 to December 31, 2009
- **Store types:** Physical stores, Online, Catalog, Reseller
- **Product categories:** Focus on electronics and home appliances
- **Scenarios:** Actual (historical), Budget (planned), Forecast (projected)

### Table Selection Guide
- Use **FactSales** for aggregated daily sales data (store + online combined)
- Use **FactOnlineSales** for transaction-level online order details with customer info
- Use **V_CustomerOrders** for basket analysis with customer demographics
- Use **V_ProductForecast** for product forecasting by category

### Important Column Notes
- **DateKey** in DimDate is datetime type, join using the Datekey column
- **DimCurrency** columns: CurrencyKey, CurrencyLabel (e.g., "USD"), CurrencyName, CurrencyDescription
- **DimCurrency does NOT have** a CurrencySymbol column - use CurrencyLabel for currency codes
- **DimGeography** has Geometry column for spatial data (may not be queryable via standard SQL)
- **DimStore** has GeoLocation and Geometry columns for spatial data
