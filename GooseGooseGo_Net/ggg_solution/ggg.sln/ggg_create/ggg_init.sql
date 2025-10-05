
USE master
GO

/*
Drop DATABASE [ggg_db]
GO
*/
if not exists (select * from sys.databases where name='ggg_db')
CREATE DATABASE [ggg_db]
GO

use ggg_db
go

if not exists (select * from sys.tables where name='tblSettings') and not exists (select * from sys.columns where name='setValue' and OBJECT_NAME(object_id)='tblSettings' and max_length=4000)
Drop TABLE [dbo].[tblSettings]
go

if not exists (select * from sys.tables where name='tblSettings')
CREATE TABLE [dbo].[tblSettings](
	[setId] [int] IDENTITY(1,1) NOT NULL,
	[setName] [nvarchar](100) NOT NULL,
	[setValue] [nvarchar](2000) NOT NULL,
	[setDescription] [nvarchar](500) NOT NULL,
 CONSTRAINT [PK_tblSettings] PRIMARY KEY CLUSTERED 
(
	[setId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO





if exists (select * from sys.procedures where name='spAssetWatchInit')
drop procedure spAssetWatchInit
go
create procedure spAssetWatchInit
as
begin

drop table tblAssetSource

drop table tblAssetWatch

if not exists (select * from sys.tables where name='tblAssetSource')
CREATE TABLE [dbo].[tblAssetSource](
	[assId] [nvarchar](20) NOT NULL,
	[assSource] [nvarchar](300) NOT NULL,
	[assDTAdded] datetime NOT NULL,
	[assEnabled] [bit] NOT NULL,
 CONSTRAINT [PK_tblAssetSource] PRIMARY KEY CLUSTERED 
(
	[assId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]


if not exists (select * from sys.tables where name='tblAssetWatch')
CREATE TABLE [dbo].[tblAssetWatch](
	[aswId] [int] IDENTITY(1,1) NOT NULL,
	[aswSourceId] [nvarchar](10) NOT NULL,
	[aswPair] [nvarchar](32) NOT NULL,
	[aswEnabled] [bit] NOT NULL,
	[aswPriceTriggerUp] [decimal](18,5) NULL,
	[aswPriceTriggerDown] [decimal](18,5) NULL,
	[aswPriceTakeProfit] [decimal](18,5) NULL,
	[aswPriceStopLoss] [decimal](18,5) NULL,

 CONSTRAINT [PK_tblAssetWatch] PRIMARY KEY CLUSTERED 
(
	[aswId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]


if not exists (select * from tblAssetSource where assId='ASS_KRAKEN')
INSERT INTO [dbo].[tblAssetSource]
           ([assId]
           ,[assSource]
           ,[assDTAdded]
           ,[assEnabled])
     VALUES
           ('ASS_KRAKEN'
           ,'KRAKEN'
           ,GETDATE()
           ,1)

if not exists (select * from tblAssetSource where assId='ASS_MEXC')
INSERT INTO [dbo].[tblAssetSource]
           ([assId]
           ,[assSource]
           ,[assDTAdded]
           ,[assEnabled])
     VALUES
           ('ASS_MEXC'
           ,'MEXC'
           ,GETDATE()
           ,1)


if not exists (select * from tblAssetWatch where aswSourceId='ASS_KRAKEN' and aswPair='MUSD')
INSERT INTO [dbo].[tblAssetWatch]
           ([aswSourceId]
           ,[aswPair]
           ,[aswEnabled]
           ,[aswPriceTriggerUp]
		   ,[aswPriceTriggerDown]
           ,[aswPriceTakeProfit]
           ,[aswPriceStopLoss])
     VALUES
           ('ASS_KRAKEN'
           ,'MUSD'
           ,1
           ,2.4
		   ,2.4
           ,2.6
           ,1.8)

if not exists (select * from tblAssetWatch where aswSourceId='ASS_MEXC' and aswPair='MYXUSD')
INSERT INTO [dbo].[tblAssetWatch]
           ([aswSourceId]
           ,[aswPair]
           ,[aswEnabled]
           ,[aswPriceTriggerUp]
		   ,[aswPriceTriggerDown]
           ,[aswPriceTakeProfit]
           ,[aswPriceStopLoss])
     VALUES
           ('ASS_MEXC'
           ,'MYXUSD'
           ,1
           ,5.7
		   ,12.0
           ,12.6
           ,1.8)
/*
if not exists (select * from tblAssetWatch where aswSourceId='ASS_KRAKEN' and aswPair='PYTHUSD')
INSERT INTO [dbo].[tblAssetWatch]
           ([aswSourceId]
           ,[aswPair]
           ,[aswEnabled]
           ,[aswPriceTriggerUp]
		   ,[aswPriceTriggerDown]
           ,[aswPriceTakeProfit]
           ,[aswPriceStopLoss])
     VALUES
           ('ASS_KRAKEN'
           ,'PYTHUSD'
           ,1
           ,0.11919
		   ,0.15392
           ,0.14919
           ,0.11919)


if not exists (select * from tblAssetWatch where aswSourceId='ASS_KRAKEN' and aswPair='PUMPUSD')
INSERT INTO [dbo].[tblAssetWatch]
           ([aswSourceId]
           ,[aswPair]
           ,[aswEnabled]
           ,[aswPriceTriggerUp]
		   ,[aswPriceTriggerDown]
           ,[aswPriceTakeProfit]
           ,[aswPriceStopLoss])
     VALUES
           ('ASS_KRAKEN'
           ,'PUMPUSD'
           ,1
           ,0.11919
		   ,0.15392
           ,0.14919
           ,0.11919)

-- delete from tblAssetWatch where aswSourceId='ASS_KRAKEN' and aswPair='PAXGUSD'
-- Track tokenised gold
if not exists (select * from tblAssetWatch where aswSourceId='ASS_KRAKEN' and aswPair='PAXGUSD')
INSERT INTO [dbo].[tblAssetWatch]
           ([aswSourceId]
           ,[aswPair]
           ,[aswEnabled]
           ,[aswPriceTriggerUp]
		   ,[aswPriceTriggerDown]
           ,[aswPriceTakeProfit]
           ,[aswPriceStopLoss])
     VALUES
           ('ASS_KRAKEN'
           ,'PAXGUSD'
           ,1
           ,3777.53
		   ,3400.00
           ,4000.00
           ,2900.00)

if not exists (select * from tblAssetWatch where aswSourceId='ASS_KRAKEN' and aswPair='IMXUSD')
INSERT INTO [dbo].[tblAssetWatch]
           ([aswSourceId]
           ,[aswPair]
           ,[aswEnabled]
           ,[aswPriceTriggerUp]
		   ,[aswPriceTriggerDown]
           ,[aswPriceTakeProfit]
           ,[aswPriceStopLoss])
     VALUES
           ('ASS_KRAKEN'
           ,'IMXUSD'
           ,1
           ,0.7599
		   ,0.9200
           ,0.9610
           ,0.7399)

if not exists (select * from tblAssetWatch where aswSourceId='ASS_KRAKEN' and aswPair='SOLUSD')
INSERT INTO [dbo].[tblAssetWatch]
           ([aswSourceId]
           ,[aswPair]
           ,[aswEnabled]
           ,[aswPriceTriggerUp]
		   ,[aswPriceTriggerDown]
           ,[aswPriceTakeProfit]
           ,[aswPriceStopLoss])
     VALUES
           ('ASS_KRAKEN'
           ,'SOLUSD'
           ,1
           ,220.19
		   ,220.19
           ,250.00
           ,200.0)


if not exists (select * from tblAssetWatch where aswSourceId='ASS_KRAKEN' and aswPair='CROUSD')
INSERT INTO [dbo].[tblAssetWatch]
           ([aswSourceId]
           ,[aswPair]
           ,[aswEnabled]
           ,[aswPriceTriggerUp]
		   ,[aswPriceTriggerDown]
           ,[aswPriceTakeProfit]
           ,[aswPriceStopLoss])
     VALUES
           ('ASS_KRAKEN'
           ,'CROUSD'
           ,1
           ,0.255
		   ,0.255
           ,0.261
           ,0.180)
*/
end
go



if exists (select * from sys.procedures where name='spAssetWatchList')
drop procedure spAssetWatchList
go
create procedure spAssetWatchList
as
begin
select [aswId]
      ,[aswSourceId]
      ,[aswPair]
	  , cast((select top 1 ka.kaLastTrade from tblKrakenAsset ka left join tblKrakenAssetInfo kai on ka.kaIndex=kai.kaiId where ka.kaPair=asw.aswPair order by kai.kaiDT desc)
	   as decimal(18,5)) as aswLastTrade
      ,[aswEnabled]
      ,[aswPriceTriggerUp]
	  ,[aswPriceTriggerDown]
      ,[aswPriceTakeProfit]
      ,[aswPriceStopLoss]
  from [dbo].[tblAssetWatch] asw
end

go



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
    kaLastTrade DECIMAL(18,5) NOT NULL,
    kaOpen DECIMAL(18,5) NULL,
    kaBid DECIMAL(18,5) NULL,
    kaAsk DECIMAL(18,5) NULL,
    kaHigh24h DECIMAL(18,5) NULL,
    kaLow24h DECIMAL(18,5) NULL,
    kaVolume24h decimal(18,5) NULL,
    kaRetrievedAt DATETIME NOT NULL,
    CONSTRAINT FK_tblKrakenAsset_kaIndex FOREIGN KEY (kaIndex)
        REFERENCES tblKrakenAssetInfo(kaiId)
        ON DELETE CASCADE
        ON UPDATE CASCADE
);

end
go


if not exists (select * from sys.tables where name='tblKrakenAsset' or name='tblKrakenAssetInfo') 
exec spKrakenRestart
go

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spKrakenRollingPercentSwing')
    DROP PROCEDURE spKrakenRollingPercentSwing
GO

CREATE PROCEDURE spKrakenRollingPercentSwing
    @kapsMinSwing DECIMAL(18,5),
    @kapsPeriodValue INT,
    @kapsPeriodUnit NVARCHAR(10),
	@kapsRowCount int,
    @kapsPeriodOffset INT
AS
BEGIN
    DECLARE @Now DATETIME = (SELECT MAX(kaiDT) FROM tblKrakenAssetInfo);
    DECLARE @IntervalStart DATETIME;
    DECLARE @IntervalEnd DATETIME;
    DECLARE @indStart INT;
    DECLARE @indEnd INT;

    -- Calculate interval start and end, supporting offset windows
    SET @IntervalEnd = 
        CASE @kapsPeriodUnit
            WHEN 'minute' THEN DATEADD(MINUTE, -(@kapsPeriodValue * @kapsPeriodOffset), @Now)
            WHEN 'hour'   THEN DATEADD(HOUR,   -(@kapsPeriodValue * @kapsPeriodOffset), @Now)
            WHEN 'day'    THEN DATEADD(DAY,    -(@kapsPeriodValue * @kapsPeriodOffset), @Now)
            WHEN 'week'   THEN DATEADD(WEEK,   -(@kapsPeriodValue * @kapsPeriodOffset), @Now)
            WHEN 'month'  THEN DATEADD(MONTH,  -(@kapsPeriodValue * @kapsPeriodOffset), @Now)
            ELSE DATEADD(MINUTE, -(@kapsPeriodValue * @kapsPeriodOffset), @Now)
        END;

    SET @IntervalStart = 
        CASE @kapsPeriodUnit
            WHEN 'minute' THEN DATEADD(MINUTE, -(@kapsPeriodValue * (1 + @kapsPeriodOffset)), @Now)
            WHEN 'hour'   THEN DATEADD(HOUR,   -(@kapsPeriodValue * (1 + @kapsPeriodOffset)), @Now)
            WHEN 'day'    THEN DATEADD(DAY,    -(@kapsPeriodValue * (1 + @kapsPeriodOffset)), @Now)
            WHEN 'week'   THEN DATEADD(WEEK,   -(@kapsPeriodValue * (1 + @kapsPeriodOffset)), @Now)
            WHEN 'month'  THEN DATEADD(MONTH,  -(@kapsPeriodValue * (1 + @kapsPeriodOffset)), @Now)
            ELSE DATEADD(MINUTE, -(@kapsPeriodValue * (1 + @kapsPeriodOffset)), @Now)
        END;

    -- Find the closest kaiId before or at each interval boundary
    SELECT TOP 1 @indStart = kaiId FROM tblKrakenAssetInfo WHERE kaiDT <= @IntervalStart ORDER BY kaiDT DESC;
    SELECT TOP 1 @indEnd = kaiId FROM tblKrakenAssetInfo WHERE kaiDT <= @IntervalEnd ORDER BY kaiDT DESC;

    -- CTEs for start and end asset data
    ;WITH windStart AS (
        SELECT * FROM tblKrakenAsset WHERE kaIndex = @indStart
    ),
    windEnd AS (
        SELECT * FROM tblKrakenAsset WHERE kaIndex = @indEnd
    )
    SELECT TOP (@kapsRowCount)
        wsStart.kaPair AS kapsPair,
        CAST(wsStart.kaLastTrade AS DECIMAL(18,5)) AS kapsStartTrade,
        CAST(wsEnd.kaLastTrade AS DECIMAL(18,5)) AS kapsEndTrade,
        -- Standard percent change calculation; NULL if not computable
        CASE 
            WHEN wsStart.kaLastTrade = 0.0 OR wsEnd.kaLastTrade = 0.0 THEN NULL
            ELSE CAST((wsEnd.kaLastTrade - wsStart.kaLastTrade) * 100.0 / wsStart.kaLastTrade AS DECIMAL(18, 3))
        END AS kapsTradeDiffPercent,
        CAST((wsEnd.kaLastTrade - wsStart.kaLastTrade) AS DECIMAL(18,5)) AS kapsTradeDiff,
        CAST(ABS(wsEnd.kaLastTrade - wsStart.kaLastTrade) AS DECIMAL(18,5)) AS kapsTradeDiffAbs,
        wsStart.kaVolume24h AS kapsStartVolume,
        wsEnd.kaVolume24h AS kapsEndVolume,
        cast(wsStart.kaRetrievedAt as datetime) AS kapsStartRetrievedAt,
        cast(wsEnd.kaRetrievedAt as datetime) AS kapsEndRetrievedAt
    FROM windStart wsStart
    INNER JOIN windEnd wsEnd ON wsStart.kaPair = wsEnd.kaPair
    -- Filter by minimum swing if computable
    WHERE 
        CASE 
            WHEN wsStart.kaLastTrade = 0.0 OR wsEnd.kaLastTrade = 0.0 THEN 0
            ELSE ABS((wsEnd.kaLastTrade - wsStart.kaLastTrade) * 100.0 / wsStart.kaLastTrade)
        END >= @kapsMinSwing
    ORDER BY
        CASE 
            WHEN wsStart.kaLastTrade = 0.0 OR wsEnd.kaLastTrade = 0.0 THEN 0
            ELSE ABS((wsEnd.kaLastTrade - wsStart.kaLastTrade) * 100.0 / wsStart.kaLastTrade)
        END DESC;
END
GO

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spKrakenRollingPercentSwing2')
    DROP PROCEDURE spKrakenRollingPercentSwing2
GO

CREATE PROCEDURE spKrakenRollingPercentSwing2
    @MinSwing DECIMAL(18, 6),
    @PeriodValue INT,
    @PeriodUnit NVARCHAR(10),
	@PeriodOffset INT

AS
BEGIN
    DECLARE @Now DATETIME = (SELECT MAX(kaiDT) FROM tblKrakenAssetInfo);
    DECLARE @IntervalStart DATETIME
    DECLARE @IntervalEnd DATETIME

	SET @IntervalEnd = 
        CASE @PeriodUnit
            WHEN 'minute' THEN DATEADD(MINUTE, -@PeriodValue * @PeriodOffset, @Now)
            WHEN 'hour'   THEN DATEADD(HOUR,   -@PeriodValue * @PeriodOffset, @Now)
            WHEN 'day'    THEN DATEADD(DAY,    -@PeriodValue * @PeriodOffset, @Now)
            WHEN 'week'   THEN DATEADD(WEEK,   -@PeriodValue * @PeriodOffset, @Now)
            WHEN 'month'  THEN DATEADD(MONTH,  -@PeriodValue * @PeriodOffset, @Now)
            ELSE DATEADD(MINUTE, -@PeriodValue * @PeriodOffset, @Now)
        END;

    SET @IntervalStart = 
        CASE @PeriodUnit
            WHEN 'minute' THEN DATEADD(MINUTE, -@PeriodValue * (1 + @PeriodOffset), @Now)
            WHEN 'hour'   THEN DATEADD(HOUR,   -@PeriodValue * (1 + @PeriodOffset), @Now)
            WHEN 'day'    THEN DATEADD(DAY,    -@PeriodValue * (1 + @PeriodOffset), @Now)
            WHEN 'week'   THEN DATEADD(WEEK,   -@PeriodValue * (1 + @PeriodOffset), @Now)
            WHEN 'month'  THEN DATEADD(MONTH,  -@PeriodValue * (1 + @PeriodOffset), @Now)
            ELSE DATEADD(MINUTE, -@PeriodValue * (1 + @PeriodOffset), @Now)
        END;

    WITH AssetInfoWindow AS (
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
        WHERE i.kaiDT BETWEEN @IntervalStart AND @IntervalEnd
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
           ,@kaLastTrade decimal(18,5)
           ,@kaOpen decimal(18,5)
           ,@kaBid decimal(18,5)
           ,@kaAsk decimal(18,5)
           ,@kaHigh24h decimal(18,5)
           ,@kaLow24h decimal(18,5)
           ,@kaVolume24h decimal(18,5)
           ,@kaRetrievedAt datetime
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
      ,max([kaRetrievedAt]) as [kaRetrievedAt]
  FROM [dbo].[tblKrakenAsset]
  group by kaPair
  order by kaPair asc
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

if exists (select * from sys.procedures where name='spSettingsInsertByName')
Drop PROCEDURE [dbo].[spSettingsInsertByName]
Go
-- =============================================
-- Author:		Tony Stoddart
-- Create date: 22/009/2025
-- Description:	Insert settings value
-- =============================================
Create PROCEDURE [dbo].[spSettingsInsertByName]
	@setName nvarchar(100)
	,@setValue nvarchar(2000)
	,@setDescription nvarchar(500)
	As
	begin
	if not exists (select * from tblSettings where setname = @setName)
	
INSERT INTO [dbo].[tblSettings]
           ([setName]
           ,[setValue]
           ,[setDescription])
     VALUES
           (@setName
           ,@setValue
           ,@setDescription
		   )


 select top 1 * from vwSettings where setName = @setName
 order by setid asc

 end
Go

if exists (select * from sys.procedures where name='spSettingsReadByName')
Drop PROCEDURE [dbo].[spSettingsReadByName]
go
-- =============================================
-- Author:		Tony Stoddart
-- Create date: 22/009/2025
-- Description:	Read by name (the first by id)
-- =============================================
Create PROCEDURE [dbo].[spSettingsReadByName]
	@setName nvarchar(100)
	As
	begin
 select top 1 * from vwSettings where setName = @setName
 order by setid asc

 end
 go

