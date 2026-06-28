# Database Schema

The application uses EF Core with `Npgsql.EntityFrameworkCore.PostgreSQL` against PostgreSQL. Production runs on Cloud SQL for PostgreSQL. The current schema is defined by [AppDbContext](C:/Users/kabal/dev/UNi/AlloyDbCrudApi/Infrastructure/Persistence/AppDbContext.cs) and the `InitialCrm` migration.

Production schema changes are applied by the `Migrate Production Database` GitHub Actions workflow. The API process does not call `Database.Migrate()` in production.

## Core Tables

### `users`, `roles`, and identity tables

ASP.NET Core Identity stores authentication and authorization data in:

- `users`
- `roles`
- `user_roles`
- `user_claims`
- `user_logins`
- `user_tokens`
- `role_claims`
- `refresh_tokens`

`users` adds business fields on top of Identity:

- `FullName`
- `IsActive`
- `CreatedAt`
- `DeactivatedAt`
- `Role`

## Reference Data

### `customers`

Operational customer dimension used by CRM and BI extraction.

Key columns:

- `CustomerId` `varchar(50)` primary key
- `Age` `integer`
- `Gender` `integer`
- `City` `varchar(120)`
- `Email` `varchar(256)`
- `IsActive` `boolean`
- `CreatedAt` `timestamptz`
- `DeletedAt` `timestamptz`

Indexes:

- `IX_customers_Email`

### `suppliers`

Supplier dimension used by products.

Key columns:

- `Id` `uuid` primary key
- `Code` `varchar(50)` unique
- `Name` `varchar(200)`
- `IsActive`
- `CreatedAt`
- `DeletedAt`

Indexes:

- `IX_suppliers_Code` unique

### `products`

Product catalog and BI product dimension source.

Key columns:

- `ProductId` `varchar(50)` primary key
- `Category` `varchar(80)`
- `Color` `varchar(50)`
- `Size` `varchar(20)`
- `Season` `varchar(40)`
- `SupplierId` `uuid` nullable foreign key to `suppliers`
- `CostPrice` `numeric(12,2)`
- `ListPrice` `numeric(12,2)`
- `IsActive`
- `CreatedAt`
- `DeletedAt`

Indexes:

- `IX_products_Category`
- `IX_products_Season`
- `IX_products_SupplierId`

### `stores`

Store and channel dimension.

Key columns:

- `StoreId` `varchar(50)` primary key
- `StoreName` `varchar(200)`
- `Region` `varchar(120)`
- `StoreSizeM2` `integer`
- `Channel` `integer`
- `IsActive`
- `CreatedAt`
- `DeletedAt`

Relationships:

- one-to-many with `inventory_items`
- one-to-many with `sales`

## Inventory And Transactions

### `inventory_items`

Current stock snapshot by store and product.

Key columns:

- `Id` `uuid` primary key
- `ProductId` `varchar(50)` foreign key to `products`
- `StoreId` `varchar(50)` foreign key to `stores`
- `StockOnHand` `integer`
- `ReservedStock` `integer`
- PostgreSQL system column `xmin` used as the EF Core concurrency token
- `UpdatedAt` `timestamptz`

Indexes:

- unique `IX_inventory_items_StoreId_ProductId`
- `IX_inventory_items_ProductId`

### `sales`

Transactional sale header table and main BI fact source.

Key columns:

- `TransactionId` `varchar(50)` primary key
- `Date` `date`
- `StoreId` `varchar(50)` foreign key to `stores`
- `CustomerId` `varchar(50)` foreign key to `customers`
- `CreatedByUserId` `uuid`
- `Status` `integer`
- `TotalRevenue` `numeric(14,2)`
- `TotalMargin` `numeric(14,2)`
- `TotalDiscount` `numeric(14,4)`
- `TotalQuantity` `integer`
- `CreatedAt` `timestamptz`

Indexes:

- `IX_sales_Date`
- `IX_sales_StoreId`
- `IX_sales_CustomerId`
- `IX_sales_Status`

### `sale_items`

Line items belonging to a sale.

Key columns:

- `Id` `uuid` primary key
- `TransactionId` `varchar(50)` foreign key to `sales`
- `ProductId` `varchar(50)` foreign key to `products`
- `Quantity` `integer`
- `Discount` `numeric(5,4)`
- `UnitListPrice` `numeric(12,2)`
- `UnitCostPrice` `numeric(12,2)`
- `Revenue` `numeric(14,2)`
- `Margin` `numeric(14,2)`

Indexes:

- `IX_sale_items_TransactionId`
- `IX_sale_items_ProductId`

### `returns`

Approved or pending returns tied one-to-one to a sale.

Key columns:

- `Id` `uuid` primary key
- `TransactionId` `varchar(50)` unique foreign key to `sales`
- `Date` `date`
- `Reason` `integer`
- `Status` `integer`
- `ApprovedByUserId` `uuid`
- `Notes` `varchar(1000)` nullable
- `CreatedAt` `timestamptz`
- `ApprovedAt` `timestamptz` nullable

Indexes:

- unique `IX_returns_TransactionId`

## Governance Tables

### `discount_policies`

Business rule configuration for margin and discount limits.

Key columns:

- `Id` `uuid` primary key
- `Name` `varchar(120)`
- `MaxDiscount` `numeric(5,4)`
- `MinMarginPercent` `numeric(5,4)`
- `RequiresSuperadminApproval` `boolean`
- `IsActive`
- `CreatedAt`

### `audit_logs`

Audit trail for security-sensitive and business-sensitive actions.

Key columns:

- `Id` `uuid` primary key
- `UserId` `uuid` nullable
- `Action` `varchar(100)`
- `EntityName` `varchar(80)`
- `EntityId` `varchar(80)`
- `Detail` `varchar(2000)` nullable
- `CorrelationId` `varchar(64)` nullable
- `CreatedAt` `timestamptz`

Indexes:

- `IX_audit_logs_EntityName`
- `IX_audit_logs_UserId`
- `IX_audit_logs_CreatedAt`

## Historical Seeding Notes

The explicit BI seed command:

```bash
dotnet run -- --seed retail-bi-history
```

creates historical operational rows in these same tables. It does not use EF data migrations. In development, automatic startup seeding remains lightweight; the large historical dataset only loads through the explicit seed command or the `Seed BI History` workflow.

## Migration History

Current migration:

- `20260622040441_InitialCrm`

To add future schema changes:

```bash
dotnet ef migrations add DescriptiveMigrationName
```

Then run the production migration workflow before deploying an app version that depends on the new schema.
