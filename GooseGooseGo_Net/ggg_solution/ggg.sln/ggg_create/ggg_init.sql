
USE master
GO

if not exists (select * from sys.databases where name='ggg_db')
CREATE DATABASE [ggg_db]
GO

use ggg_db
go

if not exists (select * from sys.tables where name='tblSettings')
CREATE TABLE [dbo].[tblSettings](
	[setId] [int] IDENTITY(1,1) NOT NULL,
	[setName] [nvarchar](100) NOT NULL,
	[setValue] [nvarchar](500) NOT NULL,
	[setDescription] [nvarchar](500) NOT NULL,
 CONSTRAINT [PK_tblSettings] PRIMARY KEY CLUSTERED 
(
	[setId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO



if exists (select * from sys.procedures where name='spKrakenRestart')
Drop Procedure spKrakenRestart
go
Create Procedure spKrakenRestart
as
begin
if exists (select * from sys.foreign_keys where name='FK_tblKrakenAssetInfo_kaId')
--if exists (select * from sys.tables where name='tblKrakenAssetInfo')
ALTER TABLE tblKrakenAssetInfo DROP CONSTRAINT FK_tblKrakenAssetInfo_kaId;
if exists (select * from sys.tables where name='tblKrakenAsset')
Drop TABLE tblKrakenAsset
if exists (select * from sys.tables where name='tblKrakenAssetInfo')
Drop TABLE [dbo].[tblKrakenAssetInfo]

CREATE TABLE tblKrakenAssetInfo (
    kaiId INT IDENTITY(1,1) PRIMARY KEY,
    kaiDT DATETIME NOT NULL
);

CREATE TABLE tblKrakenAsset (
    kaId INT IDENTITY(1,1) PRIMARY KEY,
    kaIndex INT NOT NULL, -- references kaiId in tblKrakenAssetInfo
    kaPair NVARCHAR(32) NOT NULL,
    kaLastTrade DECIMAL(38,18) NOT NULL,
    kaOpen DECIMAL(38,18) NULL,
    kaBid DECIMAL(38,18) NULL,
    kaAsk DECIMAL(38,18) NULL,
    kaHigh24h DECIMAL(38,18) NULL,
    kaLow24h DECIMAL(38,18) NULL,
    kaVolume24h NVARCHAR(64) NULL,
    kaRetrievedAt DATETIME2 NOT NULL,
    CONSTRAINT FK_tblKrakenAsset_kaIndex FOREIGN KEY (kaIndex)
        REFERENCES tblKrakenAssetInfo(kaiId)
        ON DELETE CASCADE
        ON UPDATE CASCADE
);

end
go

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spKrakenRollingPercentSwing')
    DROP PROCEDURE spKrakenRollingPercentSwing
GO

CREATE PROCEDURE spKrakenRollingPercentSwing
    @MinSwing DECIMAL(18, 6),
    @PeriodValue INT,
    @PeriodUnit NVARCHAR(10)
AS
BEGIN
    -- Use latest kaiDT from data, not the system time
    DECLARE @Now DATETIME = (SELECT MAX(kaiDT) FROM tblKrakenAssetInfo);
    DECLARE @IntervalStart DATETIME;

    SET @IntervalStart = 
        CASE @PeriodUnit
            WHEN 'minute' THEN DATEADD(MINUTE, -@PeriodValue, @Now)
            WHEN 'hour'   THEN DATEADD(HOUR,   -@PeriodValue, @Now)
            WHEN 'day'    THEN DATEADD(DAY,    -@PeriodValue, @Now)
            WHEN 'week'   THEN DATEADD(WEEK,   -@PeriodValue, @Now)
            WHEN 'month'  THEN DATEADD(MONTH,  -@PeriodValue, @Now)
            ELSE DATEADD(MINUTE, -@PeriodValue, @Now)
        END;

    ;WITH AssetInfoWindow AS (
        SELECT
            a.kaPair,
            a.kaLastTrade,
            a.kaVolume24h,
            a.kaRetrievedAt,
            i.kaiId,
            i.kaiDT,
            ROW_NUMBER() OVER (PARTITION BY a.kaPair ORDER BY i.kaiDT DESC) AS rn_latest,
            ROW_NUMBER() OVER (PARTITION BY a.kaPair ORDER BY i.kaiDT ASC) AS rn_earliest
        FROM tblKrakenAsset a
        INNER JOIN tblKrakenAssetInfo i ON a.kaIndex = i.kaiId
        WHERE i.kaiDT BETWEEN @IntervalStart AND @Now
    ),
    LatestAssetInfo AS (
        SELECT * FROM AssetInfoWindow WHERE rn_latest = 1
    ),
    AssetPeriodAgo AS (
        SELECT * FROM AssetInfoWindow WHERE rn_earliest = 1
    )
    SELECT
        l.kaPair,
        l.kaLastTrade AS LatestTrade,
        p.kaLastTrade AS TradePeriodAgo,
        l.kaVolume24h,
        l.kaiDT AS LatestKaiDT,
        p.kaiDT AS KaiDTPeriodAgo,
        CASE 
            WHEN p.kaLastTrade = 0 THEN NULL
            ELSE ((l.kaLastTrade - p.kaLastTrade) / p.kaLastTrade) * 100
        END AS PercentSwing
    FROM LatestAssetInfo l
    INNER JOIN AssetPeriodAgo p ON l.kaPair = p.kaPair
    WHERE
        p.kaLastTrade IS NOT NULL
        AND ABS(
            CASE 
                WHEN p.kaLastTrade = 0 THEN 0
                ELSE ((l.kaLastTrade - p.kaLastTrade) / p.kaLastTrade) * 100
            END
        ) >= @MinSwing
    ORDER BY ABS(
        CASE 
            WHEN p.kaLastTrade = 0 THEN 0
            ELSE ((l.kaLastTrade - p.kaLastTrade) / p.kaLastTrade) * 100
        END
    ) DESC
END
GO


if exists (select * from sys.procedures where name='spKrakenAssetInfoNextId')
Drop Procedure spKrakenAssetInfoNextId
go
Create Procedure spKrakenAssetInfoNextId
as
begin
INSERT INTO [dbo].[tblKrakenAssetInfo]
           ([kaiDT])
     VALUES
           (CURRENT_TIMESTAMP)
	select * from tblKrakenAssetInfo where kaiId=SCOPE_IDENTITY()
end
go

if exists (select * from sys.procedures where name='spKrakenUpdateById')
Drop Procedure spKrakenUpdateById
go
Create Procedure spKrakenUpdateById
           @kaId int
		   ,@kaIndex int
		   ,@kaPair nvarchar(32)
           ,@kaLastTrade decimal(38,18)
           ,@kaOpen decimal(38,18)
           ,@kaBid decimal(38,18)
           ,@kaAsk decimal(38,18)
           ,@kaHigh24h decimal(38,18)
           ,@kaLow24h decimal(38,18)
           ,@kaVolume24h nvarchar(64)
           ,@kaRetrievedAt datetime2(7)
as
begin
if not exists (select * from tblKrakenAsset where kaId = @kaId)
begin
INSERT INTO [dbo].[tblKrakenAsset]
           (
		   [kaIndex]
		   ,[kaPair]
           ,[kaLastTrade]
           ,[kaOpen]
           ,[kaBid]
           ,[kaAsk]
           ,[kaHigh24h]
           ,[kaLow24h]
           ,[kaVolume24h]
           ,[kaRetrievedAt])
     VALUES
           (
		   @kaIndex
		   ,@kaPair
           ,@kaLastTrade
           ,@kaOpen
           ,@kaBid
           ,@kaAsk
           ,@kaHigh24h
           ,@kaLow24h
           ,@kaVolume24h
           ,@kaRetrievedAt
		   )
	SET @kaId = SCOPE_IDENTITY();


end
else

UPDATE [dbo].[tblKrakenAsset]
   SET
      [kaIndex] = @kaIndex
	  ,[kaPair] = @kaPair
      ,[kaLastTrade] = @kaLastTrade
      ,[kaOpen] = @kaOpen
      ,[kaBid] = @kaBid
      ,[kaAsk] = @kaAsk
      ,[kaHigh24h] = @kaHigh24h
      ,[kaLow24h] = @kaLow24h
      ,[kaVolume24h] = @kaVolume24h
      ,[kaRetrievedAt] = @kaRetrievedAt
 WHERE kaId = @kaId

 select * from tblKrakenAsset where kaId=@kaId
 end
GO


if exists (select * from sys.procedures where name='spKrakenInfoList')
Drop Procedure spKrakenInfoList
go
Create Procedure spKrakenInfoList
as
begin
SELECT 
      [kaPair]
      ,min([kaLastTrade]) as kaMinLastTrade
	  ,max([kaLastTrade]) as kaMaxLastTrade
      --,[kaOpen]
      --,[kaBid]
      --,[kaAsk]
      ,max([kaHigh24h]) as [kaHigh24h]
      ,min([kaLow24h]) as kaLow24h
      ,max([kaVolume24h]) as kaVolume24h
      --,[kaRetrievedAt]
  FROM [dbo].[tblKrakenAsset]

  group by kaPair
end



go

if exists (select * from sys.procedures where name='spKrakenBiggestChangePer5Min')
Drop PROCEDURE spKrakenBiggestChangePer5Min
go
CREATE PROCEDURE spKrakenBiggestChangePer5Min
AS
BEGIN
    WITH TradesBy5Min AS (
        SELECT
            kaPair,
            DATEADD(
                MINUTE,
                (DATEDIFF(MINUTE, 0, kaRetrievedAt) / 5) * 5,
                0
            ) AS IntervalStart,
            MIN(kaLastTrade) AS MinLastTrade,
            MAX(kaLastTrade) AS MaxLastTrade
        FROM dbo.tblKrakenAsset
        GROUP BY
            kaPair,
            DATEADD(
                MINUTE,
                (DATEDIFF(MINUTE, 0, kaRetrievedAt) / 5) * 5,
                0
            )
    )
    SELECT
        kaPair,
        IntervalStart,
        MaxLastTrade - MinLastTrade AS BiggestChange
    FROM TradesBy5Min
    ORDER BY kaPair, IntervalStart
END
GO


if exists (select * from sys.views where name='vwSettings' )
Drop  View [dbo].[vwSettings]
go
CREATE View [dbo].[vwSettings]
as
SELECT
setId
, setName
, setValue
, setDescription
FROM            dbo.tblSettings
GO


if exists (select * from sys.procedures where name='spSettingsList')
Drop PROCEDURE [dbo].[spSettingsList]
go

-- =============================================
-- Author:		Tony Stoddart
-- Create date: 15/05/2023
-- Description:	Get settings list by value
-- =============================================
CREATE PROCEDURE [dbo].[spSettingsList]
	@setName nvarchar(100)
AS
BEGIN
	SET NOCOUNT ON;

select * from vwSettings where setName = @setName
order by setid asc

END
GO

if exists (select * from sys.procedures where name='spSettingsListAll')
Drop PROCEDURE [dbo].[spSettingsListAll]
Go
-- =============================================
-- Author:		Tony Stoddart
-- Create date: 15/05/2023
-- Description:	Get all in settings list
-- =============================================
Create PROCEDURE [dbo].[spSettingsListAll]

AS
BEGIN
	SET NOCOUNT ON;

select * from vwSettings
order by setId asc

END
GO


if exists (select * from sys.procedures where name='spSettingsUpdateById')
Drop PROCEDURE [dbo].[spSettingsUpdateById]
go
-- =============================================
-- Author:		Tony Stoddart
-- Create date: 15/05/2023
-- Description:	Update settings value
-- =============================================
Create PROCEDURE [dbo].[spSettingsUpdateById]
	@setId int
	,@setValue nvarchar(500)
	As
	begin
	if exists (select * from tblSettings where setId = @setId)
	
UPDATE [dbo].[tblSettings]
   SET [setValue] = @setValue
 WHERE [setId] = @setId

 select * from vwSettings where setId = @setId
 order by setid asc

 end
GO
