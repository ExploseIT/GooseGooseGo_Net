
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

drop table tblAssetExchange

drop table tblAssetWatch

if not exists (select * from sys.tables where name='tblAssetExchange')
CREATE TABLE [dbo].[tblAssetExchange](
	[asxId] [nvarchar](20) NOT NULL,
	[asxExchange] [nvarchar](300) NOT NULL,
	[asxDTAdded] datetime NOT NULL,
	[asxEnabled] [bit] NOT NULL,
 CONSTRAINT [PK_tblAssetSource] PRIMARY KEY CLUSTERED 
(
	[asxId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]


if not exists (select * from sys.tables where name='tblAssetWatch')
CREATE TABLE [dbo].[tblAssetWatch](
	[aswId] [int] IDENTITY(1,1) NOT NULL,
	[aswExchangeId] [nvarchar](10) NOT NULL,
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


if not exists (select * from tblAssetExchange where asxId='EXC_KRAKEN')
INSERT INTO [dbo].[tblAssetExchange]
           ([asxId]
           ,[asxExchange]
           ,[asxDTAdded]
           ,[asxEnabled])
     VALUES
           ('EXC_KRAKEN'
           ,'KRAKEN'
           ,GETDATE()
           ,1)

if not exists (select * from tblAssetExchange where asxId='EXC_MEXC')
INSERT INTO [dbo].[tblAssetExchange]
           ([asxId]
           ,[asxExchange]
           ,[asxDTAdded]
           ,[asxEnabled])
     VALUES
           ('EXC_MEXC'
           ,'MEXC'
           ,GETDATE()
           ,1)


if not exists (select * from tblAssetWatch where aswExchangeId='EXC_KRAKEN' and aswPair='MUSD')
INSERT INTO [dbo].[tblAssetWatch]
           ([aswExchangeId]
           ,[aswPair]
           ,[aswEnabled]
           ,[aswPriceTriggerUp]
		   ,[aswPriceTriggerDown]
           ,[aswPriceTakeProfit]
           ,[aswPriceStopLoss])
     VALUES
           ('EXC_KRAKEN'
           ,'MUSD'
           ,1
           ,2.4
		   ,2.4
           ,2.6
           ,1.8)

if not exists (select * from tblAssetWatch where aswExchangeId='EXC_MEXC' and aswPair='MYXUSD')
INSERT INTO [dbo].[tblAssetWatch]
           ([aswExchangeId]
           ,[aswPair]
           ,[aswEnabled]
           ,[aswPriceTriggerUp]
		   ,[aswPriceTriggerDown]
           ,[aswPriceTakeProfit]
           ,[aswPriceStopLoss])
     VALUES
           ('EXC_MEXC'
           ,'MYXUSD'
           ,1
           ,5.7
		   ,12.0
           ,12.6
           ,1.8)
/*
if not exists (select * from tblAssetWatch where aswSourceId='AST_KRAKEN' and aswPair='PYTHUSD')
INSERT INTO [dbo].[tblAssetWatch]
           ([aswSourceId]
           ,[aswPair]
           ,[aswEnabled]
           ,[aswPriceTriggerUp]
		   ,[aswPriceTriggerDown]
           ,[aswPriceTakeProfit]
           ,[aswPriceStopLoss])
     VALUES
           ('AST_KRAKEN'
           ,'PYTHUSD'
           ,1
           ,0.11919
		   ,0.15392
           ,0.14919
           ,0.11919)


if not exists (select * from tblAssetWatch where aswSourceId='AST_KRAKEN' and aswPair='PUMPUSD')
INSERT INTO [dbo].[tblAssetWatch]
           ([aswSourceId]
           ,[aswPair]
           ,[aswEnabled]
           ,[aswPriceTriggerUp]
		   ,[aswPriceTriggerDown]
           ,[aswPriceTakeProfit]
           ,[aswPriceStopLoss])
     VALUES
           ('AST_KRAKEN'
           ,'PUMPUSD'
           ,1
           ,0.11919
		   ,0.15392
           ,0.14919
           ,0.11919)

-- delete from tblAssetWatch where aswSourceId='AST_KRAKEN' and aswPair='PAXGUSD'
-- Track tokenised gold
if not exists (select * from tblAssetWatch where aswSourceId='AST_KRAKEN' and aswPair='PAXGUSD')
INSERT INTO [dbo].[tblAssetWatch]
           ([aswSourceId]
           ,[aswPair]
           ,[aswEnabled]
           ,[aswPriceTriggerUp]
		   ,[aswPriceTriggerDown]
           ,[aswPriceTakeProfit]
           ,[aswPriceStopLoss])
     VALUES
           ('AST_KRAKEN'
           ,'PAXGUSD'
           ,1
           ,3777.53
		   ,3400.00
           ,4000.00
           ,2900.00)

if not exists (select * from tblAssetWatch where aswSourceId='AST_KRAKEN' and aswPair='IMXUSD')
INSERT INTO [dbo].[tblAssetWatch]
           ([aswSourceId]
           ,[aswPair]
           ,[aswEnabled]
           ,[aswPriceTriggerUp]
		   ,[aswPriceTriggerDown]
           ,[aswPriceTakeProfit]
           ,[aswPriceStopLoss])
     VALUES
           ('AST_KRAKEN'
           ,'IMXUSD'
           ,1
           ,0.7599
		   ,0.9200
           ,0.9610
           ,0.7399)

if not exists (select * from tblAssetWatch where aswSourceId='AST_KRAKEN' and aswPair='SOLUSD')
INSERT INTO [dbo].[tblAssetWatch]
           ([aswSourceId]
           ,[aswPair]
           ,[aswEnabled]
           ,[aswPriceTriggerUp]
		   ,[aswPriceTriggerDown]
           ,[aswPriceTakeProfit]
           ,[aswPriceStopLoss])
     VALUES
           ('AST_KRAKEN'
           ,'SOLUSD'
           ,1
           ,220.19
		   ,220.19
           ,250.00
           ,200.0)


if not exists (select * from tblAssetWatch where aswSourceId='AST_KRAKEN' and aswPair='CROUSD')
INSERT INTO [dbo].[tblAssetWatch]
           ([aswSourceId]
           ,[aswPair]
           ,[aswEnabled]
           ,[aswPriceTriggerUp]
		   ,[aswPriceTriggerDown]
           ,[aswPriceTakeProfit]
           ,[aswPriceStopLoss])
     VALUES
           ('AST_KRAKEN'
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
      ,[aswExchangeId]
      ,[aswPair]
	  , cast((select top 1 ass.assLastTrade from tblAsset ass left join tblAssetInfo asi on ass.assIndex=asi.asiId where ass.assPair=asw.aswPair order by asi.asiDT desc)
	   as decimal(18,5)) as aswLastTrade
      ,[aswEnabled]
      ,[aswPriceTriggerUp]
	  ,[aswPriceTriggerDown]
      ,[aswPriceTakeProfit]
      ,[aswPriceStopLoss]
  from [dbo].[tblAssetWatch] asw
end

go



if exists (select * from sys.procedures where name='spAssetRestart')
Drop Procedure spAssetRestart
go
Create Procedure spAssetRestart
as
begin
if exists (select * from sys.foreign_keys where name='FK_tblAssetInfo_assId')
--if exists (select * from sys.tables where name='tblAssetInfo')
ALTER TABLE tblAssetInfo DROP CONSTRAINT FK_tblAssetInfo_assId;
if exists (select * from sys.tables where name='tblAsset')
Drop TABLE tblAsset
if exists (select * from sys.tables where name='tblAssetInfo')
Drop TABLE [dbo].[tblAssetInfo]

CREATE TABLE tblAssetInfo (
    asiId INT IDENTITY(1,1) PRIMARY KEY,
    asiDT DATETIME NOT NULL
);

CREATE TABLE tblAsset (
    assId BIGINT IDENTITY(1,1) PRIMARY KEY,
    assIndex INT NOT NULL, -- references asiId in tblAssetInfo
	assExchange NVARCHAR(30) NOT NULL,
    assPair NVARCHAR(32) NOT NULL,
    assLastTrade DECIMAL(18,5) NOT NULL,
    assOpen DECIMAL(18,5) NULL,
    assBid DECIMAL(18,5) NULL,
	assAsk DECIMAL(18,5) NULL,
    assHigh24h DECIMAL(18,5) NULL,
    assLow24h DECIMAL(18,5) NULL,
    assVolume24h decimal(18,5) NULL,
    assRetrievedAt DATETIME NOT NULL,
    CONSTRAINT FK_tblAsset_assIndex FOREIGN KEY (assIndex)
        REFERENCES tblAssetInfo(asiId)
        ON DELETE CASCADE
        ON UPDATE CASCADE
);

end
go


if not exists (select * from sys.tables where name='tblAsset' or name='tblAssetInfo') 
exec spAssetRestart
go

IF EXISTS (SELECT * FROM sys.procedures WHERE name = 'spAssetRollingPercentSwing')
    DROP PROCEDURE spAssetRollingPercentSwing
GO

CREATE PROCEDURE spAssetRollingPercentSwing
	@aspspUpSwing bit,
    @aspspMinSwing DECIMAL(18,5),
    @aspspPeriodValue INT,
    @aspspPeriodUnit NVARCHAR(10),
	@aspspRowCount int,
    @aspspPeriodOffset INT
AS
BEGIN
    DECLARE @Now DATETIME = (SELECT MAX(asiDT) FROM tblAssetInfo);
    DECLARE @IntervalStart DATETIME;
    DECLARE @IntervalEnd DATETIME;
    DECLARE @indStart INT;
    DECLARE @indEnd INT;

    -- Calculate interval start and end, supporting offset windows
    SET @IntervalEnd = 
        CASE @aspspPeriodUnit
            WHEN 'minute' THEN DATEADD(MINUTE, -(@aspspPeriodValue * @aspspPeriodOffset), @Now)
            WHEN 'hour'   THEN DATEADD(HOUR,   -(@aspspPeriodValue * @aspspPeriodOffset), @Now)
            WHEN 'day'    THEN DATEADD(DAY,    -(@aspspPeriodValue * @aspspPeriodOffset), @Now)
            WHEN 'week'   THEN DATEADD(WEEK,   -(@aspspPeriodValue * @aspspPeriodOffset), @Now)
            WHEN 'month'  THEN DATEADD(MONTH,  -(@aspspPeriodValue * @aspspPeriodOffset), @Now)
            ELSE DATEADD(MINUTE, -(@aspspPeriodValue * @aspspPeriodOffset), @Now)
        END;

    SET @IntervalStart = 
        CASE @aspspPeriodUnit
            WHEN 'minute' THEN DATEADD(MINUTE, -(@aspspPeriodValue * (1 + @aspspPeriodOffset)), @Now)
            WHEN 'hour'   THEN DATEADD(HOUR,   -(@aspspPeriodValue * (1 + @aspspPeriodOffset)), @Now)
            WHEN 'day'    THEN DATEADD(DAY,    -(@aspspPeriodValue * (1 + @aspspPeriodOffset)), @Now)
            WHEN 'week'   THEN DATEADD(WEEK,   -(@aspspPeriodValue * (1 + @aspspPeriodOffset)), @Now)
            WHEN 'month'  THEN DATEADD(MONTH,  -(@aspspPeriodValue * (1 + @aspspPeriodOffset)), @Now)
            ELSE DATEADD(MINUTE, -(@aspspPeriodValue * (1 + @aspspPeriodOffset)), @Now)
        END;

    -- Find the closest asiId before or at each interval boundary
    SELECT TOP 1 @indStart = asiId FROM tblAssetInfo WHERE asiDT <= @IntervalStart ORDER BY asiDT DESC;
    SELECT TOP 1 @indEnd = asiId FROM tblAssetInfo WHERE asiDT <= @IntervalEnd ORDER BY asiDT DESC;

    -- CTEs for start and end asset data
    ;WITH windStart AS (
        SELECT * FROM tblAsset WHERE assIndex = @indStart
    ),
    windEnd AS (
        SELECT * FROM tblAsset WHERE assIndex = @indEnd
    )
    SELECT TOP (@aspspRowCount)
		wsStart.assId as asspsId,
        wsStart.assPair AS asspsPair,
		wsStart.assExchange as asspsExchange,
		exc.asxExchange as asspsExchangeFullName,
        CAST(wsStart.assLastTrade AS DECIMAL(18,5)) AS asspsStartTrade,
        CAST(wsEnd.assLastTrade AS DECIMAL(18,5)) AS asspsEndTrade,
        -- Standard percent change calculation; NULL if not computable
        CASE 
            WHEN wsStart.assLastTrade = 0.0 OR wsEnd.assLastTrade = 0.0 THEN NULL
            ELSE CAST((wsEnd.assLastTrade - wsStart.assLastTrade) * 100.0 / wsStart.assLastTrade AS DECIMAL(18, 3))
        END AS asspsTradeDiffPercent,
        CAST((wsEnd.assLastTrade - wsStart.assLastTrade) AS DECIMAL(18,5)) AS asspsTradeDiff,
        CAST(ABS(wsEnd.assLastTrade - wsStart.assLastTrade) AS DECIMAL(18,5)) AS asspsTradeDiffAbs,
        wsStart.assVolume24h AS asspsStartVolume,
        wsEnd.assVolume24h AS asspsEndVolume,
        cast(wsStart.assRetrievedAt as datetime) AS asspsStartRetrievedAt,
        cast(wsEnd.assRetrievedAt as datetime) AS asspsEndRetrievedAt
    FROM windStart wsStart
    INNER JOIN windEnd wsEnd ON wsStart.assPair = wsEnd.assPair
	inner join tblAssetExchange exc on wsStart.assExchange = exc.asxId
    -- Filter by minimum swing if computable
    WHERE 
        CASE 
            
			when @aspspUpSwing=1 and wsEnd.assLastTrade > wsStart.assLastTrade
			then ABS((wsEnd.assLastTrade - wsStart.assLastTrade) * 100.0 / wsStart.assLastTrade)
			when @aspspUpSwing=0 and wsStart.assLastTrade > wsEnd.assLastTrade
            then ABS((wsStart.assLastTrade - wsEnd.assLastTrade) * 100.0 / wsStart.assLastTrade)
			when wsStart.assLastTrade = 0.0 OR wsEnd.assLastTrade = 0.0 THEN 0
			else 0.0
        END >= @aspspMinSwing
    ORDER BY
        CASE 
            WHEN wsStart.assLastTrade = 0.0 OR wsEnd.assLastTrade = 0.0 THEN 0
            ELSE ABS((wsEnd.assLastTrade - wsStart.assLastTrade) * 100.0 / wsStart.assLastTrade)
        END DESC;
END
GO


if exists (select * from sys.procedures where name='spAssetInfoNextId')
Drop Procedure spAssetInfoNextId
go
Create Procedure spAssetInfoNextId
as
begin
INSERT INTO [dbo].[tblAssetInfo]
           ([asiDT])
     VALUES
           (CURRENT_TIMESTAMP)
	select * from tblAssetInfo where asiId=SCOPE_IDENTITY()
end
go

if exists (select * from sys.procedures where name='spAssetUpdateById')
Drop Procedure spAssetUpdateById
go
Create Procedure spAssetUpdateById
           @assId int
		   ,@assIndex int
		   ,@assExchange nvarchar(30)
		   ,@assPair nvarchar(32)
           ,@assLastTrade decimal(18,5)
           ,@assOpen decimal(18,5)
           ,@assBid decimal(18,5)
           ,@assAsk decimal(18,5)
           ,@assHigh24h decimal(18,5) null
           ,@assLow24h decimal(18,5)
           ,@assVolume24h decimal(18,5)
           ,@assRetrievedAt datetime
as
begin
if (@assVolume24h = 0.0 or @assHigh24h is null or @assLastTrade = 0.0)
 select * from tblAsset where assId=@assId
else
if not exists (select * from tblAsset where assId = @assId)
begin
INSERT INTO [dbo].[tblAsset]
           (
		   [assIndex]
		   ,[assExchange]
		   ,[assPair]
           ,[assLastTrade]
           ,[assOpen]
           ,[assBid]
           ,[assAsk]
           ,[assHigh24h]
           ,[assLow24h]
           ,[assVolume24h]
           ,[assRetrievedAt])
     VALUES
           (
		   @assIndex
		   ,@assExchange
		   ,@assPair
           ,@assLastTrade
           ,@assOpen
           ,@assBid
           ,@assAsk
           ,@assHigh24h
           ,@assLow24h
           ,@assVolume24h
           ,@assRetrievedAt
		   )
	SET @assId = SCOPE_IDENTITY();


end
else

UPDATE [dbo].[tblAsset]
   SET
      [assIndex] = @assIndex
	  ,[assExchange] = @assExchange
	  ,[assPair] = @assPair
      ,[assLastTrade] = @assLastTrade
      ,[assOpen] = @assOpen
      ,[assBid] = @assBid
      ,[assAsk] = @assAsk
      ,[assHigh24h] = @assHigh24h
      ,[assLow24h] = @assLow24h
      ,[assVolume24h] = @assVolume24h
      ,[assRetrievedAt] = @assRetrievedAt
 WHERE assId = @assId

 select * from tblAsset where assId=@assId
 end
GO


if exists (select * from sys.procedures where name='spAssetInfoList')
Drop Procedure spAssetInfoList
go
Create Procedure spAssetInfoList
as
begin
SELECT 
      [assPair]
      ,min([assLastTrade]) as assMinLastTrade
	  ,max([assLastTrade]) as assMaxLastTrade
      --,[assOpen]
      --,[assBid]
      --,[assAsk]
      ,max([assHigh24h]) as [assHigh24h]
      ,min([assLow24h]) as assLow24h
      ,max([assVolume24h]) as assVolume24h
      ,max([assRetrievedAt]) as [assRetrievedAt]
  FROM [dbo].[tblAsset]
  group by assPair
  order by assPair asc
end

go

if exists (select * from sys.procedures where name='spAssetBiggestChangePer5Min')
Drop PROCEDURE spAssetBiggestChangePer5Min
go
CREATE PROCEDURE spAssetBiggestChangePer5Min
AS
BEGIN
    WITH TradesBy5Min AS (
        SELECT
            assPair,
            DATEADD(
                MINUTE,
                (DATEDIFF(MINUTE, 0, assRetrievedAt) / 5) * 5,
                0
            ) AS IntervalStart,
            MIN(assLastTrade) AS MinLastTrade,
            MAX(assLastTrade) AS MaxLastTrade
        FROM dbo.tblAsset
        GROUP BY
            assPair,
            DATEADD(
                MINUTE,
                (DATEDIFF(MINUTE, 0, assRetrievedAt) / 5) * 5,
                0
            )
    )
    SELECT
        assPair,
        IntervalStart,
        MaxLastTrade - MinLastTrade AS BiggestChange
    FROM TradesBy5Min
    ORDER BY assPair, IntervalStart
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

