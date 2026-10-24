CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) NOT NULL,
    `ProductVersion` varchar(32) NOT NULL,
    PRIMARY KEY (`MigrationId`)
);

START TRANSACTION;
CREATE TABLE `platform_audit_events` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `OccurredAtUtc` datetime(6) NOT NULL,
    `Action` varchar(50) NOT NULL,
    `UserId` bigint NULL,
    `TenantId` bigint NULL,
    `Detail` varchar(200) NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `tenants` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `Code` varchar(50) NOT NULL,
    `Name` varchar(150) NOT NULL,
    `Status` varchar(20) NOT NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `users` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `DisplayName` varchar(150) NOT NULL,
    `IsActive` tinyint(1) NOT NULL,
    `RequiresPasswordChange` tinyint(1) NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `UserName` varchar(256) NULL,
    `NormalizedUserName` varchar(256) NULL,
    `Email` varchar(256) NULL,
    `NormalizedEmail` varchar(256) NULL,
    `EmailConfirmed` tinyint(1) NOT NULL,
    `PasswordHash` longtext NULL,
    `SecurityStamp` longtext NULL,
    `ConcurrencyStamp` longtext NULL,
    `PhoneNumber` longtext NULL,
    `PhoneNumberConfirmed` tinyint(1) NOT NULL,
    `TwoFactorEnabled` tinyint(1) NOT NULL,
    `LockoutEnd` datetime NULL,
    `LockoutEnabled` tinyint(1) NOT NULL,
    `AccessFailedCount` int NOT NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `roles` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `TenantId` bigint NOT NULL,
    `Name` varchar(50) NOT NULL,
    `IsTemplate` tinyint(1) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_roles_tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `tenants` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `tenant_databases` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `TenantId` bigint NOT NULL,
    `Server` varchar(255) NOT NULL,
    `Port` int NOT NULL,
    `DatabaseName` varchar(64) NOT NULL,
    `SecretName` varchar(100) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_tenant_databases_tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `tenants` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `tenant_modules` (
    `TenantId` bigint NOT NULL,
    `Module` varchar(50) NOT NULL,
    `Enabled` tinyint(1) NOT NULL,
    PRIMARY KEY (`TenantId`, `Module`),
    CONSTRAINT `FK_tenant_modules_tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `tenants` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `refresh_tokens` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `UserId` bigint NOT NULL,
    `TenantId` bigint NULL,
    `TokenHash` char(64) NOT NULL,
    `FamilyId` char(36) NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `ExpiresAtUtc` datetime(6) NOT NULL,
    `RevokedAtUtc` datetime(6) NULL,
    `RevokedReason` varchar(30) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_refresh_tokens_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `user_claims` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `UserId` bigint NOT NULL,
    `ClaimType` longtext NULL,
    `ClaimValue` longtext NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_user_claims_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `user_logins` (
    `LoginProvider` varchar(128) NOT NULL,
    `ProviderKey` varchar(128) NOT NULL,
    `ProviderDisplayName` longtext NULL,
    `UserId` bigint NOT NULL,
    PRIMARY KEY (`LoginProvider`, `ProviderKey`),
    CONSTRAINT `FK_user_logins_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `user_tokens` (
    `UserId` bigint NOT NULL,
    `LoginProvider` varchar(128) NOT NULL,
    `Name` varchar(128) NOT NULL,
    `Value` longtext NULL,
    PRIMARY KEY (`UserId`, `LoginProvider`, `Name`),
    CONSTRAINT `FK_user_tokens_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE CASCADE
);

CREATE TABLE `memberships` (
    `Id` bigint NOT NULL AUTO_INCREMENT,
    `UserId` bigint NOT NULL,
    `TenantId` bigint NOT NULL,
    `RoleId` bigint NOT NULL,
    `Status` varchar(20) NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_memberships_roles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `roles` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_memberships_tenants_TenantId` FOREIGN KEY (`TenantId`) REFERENCES `tenants` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_memberships_users_UserId` FOREIGN KEY (`UserId`) REFERENCES `users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `role_permissions` (
    `RoleId` bigint NOT NULL,
    `Permission` varchar(100) NOT NULL,
    PRIMARY KEY (`RoleId`, `Permission`),
    CONSTRAINT `FK_role_permissions_roles_RoleId` FOREIGN KEY (`RoleId`) REFERENCES `roles` (`Id`) ON DELETE CASCADE
);

CREATE INDEX `IX_memberships_RoleId` ON `memberships` (`RoleId`);

CREATE INDEX `IX_memberships_TenantId` ON `memberships` (`TenantId`);

CREATE UNIQUE INDEX `IX_memberships_UserId_TenantId` ON `memberships` (`UserId`, `TenantId`);

CREATE INDEX `IX_platform_audit_events_OccurredAtUtc` ON `platform_audit_events` (`OccurredAtUtc`);

CREATE INDEX `IX_refresh_tokens_FamilyId` ON `refresh_tokens` (`FamilyId`);

CREATE UNIQUE INDEX `IX_refresh_tokens_TokenHash` ON `refresh_tokens` (`TokenHash`);

CREATE INDEX `IX_refresh_tokens_UserId` ON `refresh_tokens` (`UserId`);

CREATE UNIQUE INDEX `IX_roles_TenantId_Name` ON `roles` (`TenantId`, `Name`);

CREATE UNIQUE INDEX `IX_tenant_databases_TenantId` ON `tenant_databases` (`TenantId`);

CREATE UNIQUE INDEX `IX_tenants_Code` ON `tenants` (`Code`);

CREATE INDEX `IX_user_claims_UserId` ON `user_claims` (`UserId`);

CREATE INDEX `IX_user_logins_UserId` ON `user_logins` (`UserId`);

CREATE INDEX `EmailIndex` ON `users` (`NormalizedEmail`);

CREATE UNIQUE INDEX `UserNameIndex` ON `users` (`NormalizedUserName`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261022193258_InitialControl', '10.0.13');

COMMIT;

