-- V_SalesAnalysis — general-purpose sales reporting view (POC)
--
-- Pre-joins FactSales to all 8 of its dimensions and exposes only clean,
-- unambiguous, business-named columns, to reduce text-to-SQL hallucination on
-- the landmine columns documented in ISSUES.md:
--   - DimGeography.RegionCountryName (not CountryRegionName / DimSalesTerritory.SalesTerritoryCountry)
--   - DimDate.CalendarQuarterLabel / CalendarMonthLabel (not the YYYYQ/YYYYMM integer columns)
--   - DimCurrency.CurrencyName (not CurrencyLabel, which is a numeric ETL code, not a display code)
--
-- Idempotent — safe to re-run.
--
-- Usage (samurai_reader already has schema-level SELECT via init-contoso-db.sql,
-- which covers future objects automatically; this script still grants explicitly
-- for self-documentation):
--   docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd \
--     -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C \
--     -i /scripts/create-sales-analysis-view.sql

USE [ContosoRetailDW]
GO

CREATE OR ALTER VIEW dbo.V_SalesAnalysis AS
SELECT
    d.CalendarYear              AS Year,
    d.CalendarQuarterLabel      AS Quarter,
    d.CalendarMonthLabel        AS MonthName,
    d.Datekey                   AS SaleDate,
    ch.ChannelName              AS ChannelName,
    st.StoreName                AS StoreName,
    geo.ContinentName           AS ContinentName,
    geo.RegionCountryName       AS CountryName,
    geo.StateProvinceName       AS StateProvinceName,
    geo.CityName                AS CityName,
    cat.ProductCategoryName     AS CategoryName,
    sub.ProductSubcategoryName  AS SubcategoryName,
    p.ProductName               AS ProductName,
    p.BrandName                 AS BrandName,
    cur.CurrencyName            AS CurrencyName,
    promo.PromotionName         AS PromotionName,
    f.SalesQuantity             AS SalesQuantity,
    f.SalesAmount               AS SalesAmount,
    f.TotalCost                 AS TotalCost,
    f.ReturnQuantity            AS ReturnQuantity,
    f.ReturnAmount              AS ReturnAmount,
    f.DiscountQuantity          AS DiscountQuantity,
    f.DiscountAmount            AS DiscountAmount
FROM dbo.FactSales f
JOIN dbo.DimDate d                 ON f.DateKey = d.Datekey
JOIN dbo.DimChannel ch             ON f.channelKey = ch.ChannelKey
JOIN dbo.DimStore st                ON f.StoreKey = st.StoreKey
JOIN dbo.DimGeography geo          ON st.GeographyKey = geo.GeographyKey
JOIN dbo.DimProduct p               ON f.ProductKey = p.ProductKey
JOIN dbo.DimProductSubcategory sub  ON p.ProductSubcategoryKey = sub.ProductSubcategoryKey
JOIN dbo.DimProductCategory cat     ON sub.ProductCategoryKey = cat.ProductCategoryKey
JOIN dbo.DimCurrency cur            ON f.CurrencyKey = cur.CurrencyKey
LEFT JOIN dbo.DimPromotion promo    ON f.PromotionKey = promo.PromotionKey
GO

GRANT SELECT ON dbo.V_SalesAnalysis TO [samurai_reader];
GO

PRINT 'V_SalesAnalysis view created/updated.'
GO
