/*
  Assign reviews whose source customer has no public name to one non-login
  profile called Anonymouse. Run once with sqlcmd against the intended store.
  The backup table named in the result contains every changed review row.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @stamp nvarchar(32) = CONVERT(nvarchar(8), SYSUTCDATETIME(), 112)
    + REPLACE(CONVERT(nvarchar(8), SYSUTCDATETIME(), 108), ':', '');
DECLARE @backupTable sysname = N'HoodAnonymouseReviewBackup_' + @stamp;
DECLARE @sql nvarchar(max);
DECLARE @anonymouseId int;
DECLARE @earliestReviewUtc datetime2;
DECLARE @registeredRoleId int;
DECLARE @storeId int;

BEGIN TRANSACTION;

SELECT @earliestReviewUtc = MIN(pr.CreatedOnUtc)
FROM ProductReview pr
INNER JOIN Customer sourceCustomer ON sourceCustomer.Id = pr.CustomerId
WHERE NULLIF(LTRIM(RTRIM(ISNULL(sourceCustomer.FirstName, N''))), N'') IS NULL
  AND NULLIF(LTRIM(RTRIM(ISNULL(sourceCustomer.LastName, N''))), N'') IS NULL
  AND NULLIF(LTRIM(RTRIM(ISNULL(sourceCustomer.Username, N''))), N'') IS NULL;

IF @earliestReviewUtc IS NULL
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50001, 'No unnamed product reviews were found. No data was changed.', 1;
END;

SELECT @anonymouseId = Id
FROM Customer
WHERE LOWER(ISNULL(Username, N'')) = N'anonymouse'
   OR LOWER(ISNULL(Email, N'')) = N'anonymouse@hoodarcheryshop.invalid';

IF @anonymouseId IS NULL
BEGIN
    SELECT TOP 1 @storeId = Id FROM Store ORDER BY Id;

    INSERT INTO Customer
        (CustomerGuid, Username, Email, IsTaxExempt, AffiliateId, VendorId,
         HasShoppingCartItems, RequireReLogin, FailedLoginAttempts, Active,
         Deleted, IsSystemAccount, CreatedOnUtc, LastActivityDateUtc,
         RegisteredInStoreId, FirstName, LastName)
    VALUES
        (NEWID(), N'Anonymouse', N'anonymouse@hoodarcheryshop.invalid', 0, 0, 0,
         0, 0, 0, 1, 0, 0, @earliestReviewUtc, @earliestReviewUtc,
         @storeId, N'Anonymouse', N'');

    SET @anonymouseId = SCOPE_IDENTITY();
END;

SELECT @registeredRoleId = Id
FROM CustomerRole
WHERE SystemName = N'Registered';

IF @registeredRoleId IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Customer_CustomerRole_Mapping WHERE Customer_Id = @anonymouseId AND CustomerRole_Id = @registeredRoleId)
BEGIN
    INSERT INTO Customer_CustomerRole_Mapping (Customer_Id, CustomerRole_Id)
    VALUES (@anonymouseId, @registeredRoleId);
END;

UPDATE Customer
SET FirstName = N'Anonymouse',
    LastName = N'',
    Username = N'Anonymouse',
    Active = 1,
    Deleted = 0,
    CreatedOnUtc = @earliestReviewUtc,
    LastActivityDateUtc = @earliestReviewUtc
WHERE Id = @anonymouseId;

SET @sql = N'
SELECT SYSUTCDATETIME() AS BackupCreatedOnUtc, pr.Id AS ProductReviewId,
       pr.CustomerId AS PreviousCustomerId, ' + CONVERT(nvarchar(16), @anonymouseId) + N' AS NewCustomerId
INTO ' + QUOTENAME(@backupTable) + N'
FROM ProductReview pr
INNER JOIN Customer sourceCustomer ON sourceCustomer.Id = pr.CustomerId
WHERE NULLIF(LTRIM(RTRIM(ISNULL(sourceCustomer.FirstName, N''''))), N'''') IS NULL
  AND NULLIF(LTRIM(RTRIM(ISNULL(sourceCustomer.LastName, N''''))), N'''') IS NULL
  AND NULLIF(LTRIM(RTRIM(ISNULL(sourceCustomer.Username, N''''))), N'''') IS NULL
  AND pr.CustomerId <> ' + CONVERT(nvarchar(16), @anonymouseId) + N';';
EXEC sp_executesql @sql;

UPDATE pr
SET CustomerId = @anonymouseId
FROM ProductReview pr
INNER JOIN Customer sourceCustomer ON sourceCustomer.Id = pr.CustomerId
WHERE NULLIF(LTRIM(RTRIM(ISNULL(sourceCustomer.FirstName, N''))), N'') IS NULL
  AND NULLIF(LTRIM(RTRIM(ISNULL(sourceCustomer.LastName, N''))), N'') IS NULL
  AND NULLIF(LTRIM(RTRIM(ISNULL(sourceCustomer.Username, N''))), N'') IS NULL
  AND pr.CustomerId <> @anonymouseId;

DECLARE @updatedRows int = @@ROWCOUNT;
COMMIT TRANSACTION;

SELECT @anonymouseId AS AnonymouseCustomerId,
       @earliestReviewUtc AS ProfileJoinDateUtc,
       @updatedRows AS ReassignedReviewRows,
       @backupTable AS BackupTable;
