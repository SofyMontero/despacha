-- Módulo Catalog (historial __ef_catalog)
CREATE TABLE IF NOT EXISTS `__ef_catalog` (
    `MigrationId` varchar(150) NOT NULL,
    `ProductVersion` varchar(32) NOT NULL,
    PRIMARY KEY (`MigrationId`)
);

START TRANSACTION;
CREATE TABLE `catalog_categories` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `Name` varchar(100) NOT NULL,
    `ImageKey` varchar(300) NULL,
    `IsActive` tinyint(1) NOT NULL,
    `Version` int NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `CreatedBy` bigint NULL,
    `UpdatedAtUtc` datetime(6) NOT NULL,
    `UpdatedBy` bigint NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `catalog_products` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `Code` varchar(50) NOT NULL,
    `Name` varchar(200) NOT NULL,
    `CategoryId` bigint NULL,
    `UnitPrice` decimal(12,2) NOT NULL,
    `PackPrice` decimal(12,2) NOT NULL,
    `UnitsPerPack` int NOT NULL,
    `SellsByUnit` tinyint(1) NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `ImageKey` varchar(300) NULL,
    `Version` int NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `CreatedBy` bigint NULL,
    `UpdatedAtUtc` datetime(6) NOT NULL,
    `UpdatedBy` bigint NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_catalog_products_catalog_categories_CategoryId` FOREIGN KEY (`CategoryId`) REFERENCES `catalog_categories` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `catalog_promotions` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `Title` varchar(150) NOT NULL,
    `Description` varchar(1000) NOT NULL,
    `Code` varchar(50) NOT NULL,
    `ProductId` bigint NULL,
    `UnitPrice` decimal(12,2) NOT NULL,
    `PackPrice` decimal(12,2) NOT NULL,
    `SellsByUnit` tinyint(1) NOT NULL,
    `Priority` int NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `ImageKey` varchar(300) NULL,
    `Version` int NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `CreatedBy` bigint NULL,
    `UpdatedAtUtc` datetime(6) NOT NULL,
    `UpdatedBy` bigint NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_catalog_promotions_catalog_products_ProductId` FOREIGN KEY (`ProductId`) REFERENCES `catalog_products` (`Id`) ON DELETE RESTRICT
);

CREATE INDEX `IX_catalog_categories_Name` ON `catalog_categories` (`Name`);

CREATE INDEX `IX_catalog_products_CategoryId` ON `catalog_products` (`CategoryId`);

CREATE INDEX `IX_catalog_products_Code` ON `catalog_products` (`Code`);

CREATE INDEX `IX_catalog_products_Name` ON `catalog_products` (`Name`);

CREATE INDEX `IX_catalog_promotions_Code` ON `catalog_promotions` (`Code`);

CREATE INDEX `IX_catalog_promotions_IsActive_Priority` ON `catalog_promotions` (`IsActive`, `Priority`);

CREATE INDEX `IX_catalog_promotions_ProductId` ON `catalog_promotions` (`ProductId`);

INSERT INTO `__ef_catalog` (`MigrationId`, `ProductVersion`)
VALUES ('20261024163423_InitialCatalog', '10.0.13');

COMMIT;


-- Módulo Customers (historial __ef_customers)
CREATE TABLE IF NOT EXISTS `__ef_customers` (
    `MigrationId` varchar(150) NOT NULL,
    `ProductVersion` varchar(32) NOT NULL,
    PRIMARY KEY (`MigrationId`)
);

START TRANSACTION;
CREATE TABLE `customers_establishments` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `Name` varchar(150) NOT NULL,
    `Address` varchar(255) NULL,
    `IsActive` tinyint(1) NOT NULL,
    `Version` int NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `CreatedBy` bigint NULL,
    `UpdatedAtUtc` datetime(6) NOT NULL,
    `UpdatedBy` bigint NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `customers_customers` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `Name` varchar(150) NOT NULL,
    `Identification` varchar(30) NULL,
    `Phone` varchar(16) NOT NULL,
    `Address` varchar(255) NULL,
    `Zone` varchar(60) NULL,
    `EstablishmentId` bigint NULL,
    `RegisteredAtUtc` datetime(6) NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `Version` int NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `CreatedBy` bigint NULL,
    `UpdatedAtUtc` datetime(6) NOT NULL,
    `UpdatedBy` bigint NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_customers_customers_customers_establishments_EstablishmentId` FOREIGN KEY (`EstablishmentId`) REFERENCES `customers_establishments` (`Id`) ON DELETE RESTRICT
);

CREATE INDEX `IX_customers_customers_EstablishmentId` ON `customers_customers` (`EstablishmentId`);

CREATE INDEX `IX_customers_customers_Identification` ON `customers_customers` (`Identification`);

CREATE INDEX `IX_customers_customers_Name` ON `customers_customers` (`Name`);

CREATE INDEX `IX_customers_customers_Phone` ON `customers_customers` (`Phone`);

CREATE INDEX `IX_customers_customers_Zone` ON `customers_customers` (`Zone`);

CREATE INDEX `IX_customers_establishments_Name` ON `customers_establishments` (`Name`);

INSERT INTO `__ef_customers` (`MigrationId`, `ProductVersion`)
VALUES ('20261024163433_InitialCustomers', '10.0.13');

COMMIT;


