
use ggg_db
go

-- drop table [dbo].[tblAssetProfit]
if not exists (select * from sys.tables where name='tblAssetProfit')
CREATE TABLE [dbo].[tblAssetProfit](
	[assprId] [int] IDENTITY(1,1) PRIMARY KEY,
	[assprAsset] [nvarchar](50) NOT NULL,
	[assprExchangeId] [nvarchar](20) NOT NULL,
	[assprPrice] [decimal](18, 5) NOT NULL,
	[assprOrderId] [nvarchar](30) NOT NULL,
	[assprDT] datetime
	CONSTRAINT FK_tblAssetProfit_asxId FOREIGN KEY (assprExchangeId)
        REFERENCES tblAssetExchange(asxId)
        ON DELETE CASCADE
        ON UPDATE CASCADE
)
GO
--[asxId] [nvarchar](20) NOT NULL,

if exists (select * from sys.procedures where name='spAssetProfitRead')
drop procedure spAssetProfitRead
go
create procedure spAssetProfitRead
	@assprAsset nvarchar(50),
	@assprExchangeId nvarchar(20),
	@assprOrderId nvarchar(30)
as
begin
select top 1 [assprId]
      ,[assprAsset]
      ,[assprExchangeId]
      ,[assprPrice]
	  ,[assprOrderId]
	  ,[assprDT]
  from [dbo].[tblAssetProfit]
  where @assprAsset=assprAsset and @assprExchangeId=assprExchangeId and @assprOrderId = assprOrderId
  order by assprDT desc
end
go


if exists (select * from sys.procedures where name='spAssetProfitUpdate')
drop procedure spAssetProfitUpdate
go
create procedure spAssetProfitUpdate
	@assprAsset nvarchar(50),
	@assprExchangeId nvarchar(20),
	@assprPrice decimal(18,5),
	@assprOrderId nvarchar(30)
as
begin
if not exists (select * from tblAssetProfit where assprOrderId=@assprOrderId and assprAsset=@assprAsset and assprExchangeId=@assprExchangeId)
insert into [dbo].[tblAssetProfit]
           ([assprAsset]
           ,[assprExchangeId]
           ,[assprPrice]
		   ,[assprOrderId]
		   ,[assprDT]
		   )
  values
           (
		   @assprAsset
           ,@assprExchangeId
		   ,@assprPrice
		   ,@assprOrderId
		   ,getdate()
		   )
else if exists (select * from tblAssetProfit where assprOrderId=@assprOrderId and assprAsset=@assprAsset and assprExchangeId=@assprExchangeId)
update [dbo].[tblAssetProfit]
   SET [assprAsset] = @assprAsset
      ,[assprExchangeId] = @assprExchangeId
      ,[assprPrice] = @assprPrice
      ,[assprOrderId] = @assprOrderId
 where assprOrderId=@assprOrderId and assprAsset=@assprAsset and assprExchangeId=@assprExchangeId
select * from tblAssetProfit where assprOrderId=@assprOrderId and assprAsset=@assprAsset and assprExchangeId=@assprExchangeId
end
go

if exists (select * from sys.procedures where name='spAssetProfitListByExchange')
drop procedure spAssetProfitListByExchange
go
create procedure spAssetProfitListByExchange
	@assprExchangeId nvarchar(20)
as
begin
select [assprId]
      ,[assprAsset]
      ,[assprExchangeId]
      ,[assprPrice]
	  ,[assprOrderId]
	  ,[assprDT]
  from [dbo].[tblAssetProfit]
  where assprExchangeId = @assprExchangeId
  order by assprDT desc
end
go
