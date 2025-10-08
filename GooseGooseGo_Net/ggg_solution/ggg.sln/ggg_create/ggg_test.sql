
use ggg_db
go

/*
select COUNT (*) from tblAsset
select COUNT (*) from tblAssetInfo
select top 5 * from tblAsset order by assRetrievedAt desc
select top 200 * from tblAssetInfo order by asiDT desc

*/
select * from tblSettings

delete from tblAsset where assVolume24h = 0.0 or assHigh24h is null or assLastTrade = 0.0
select * from tblAsset where assVolume24h = 0.0 or assHigh24h is null or assLastTrade = 0.0

select 
(select assLastTrade from tblAsset where assPair='MUSD' and assIndex=(select top 1 asiId from tblAssetInfo order by asiDT desc))
, (select COUNT (*) from tblAssetInfo) as [Row Count]

select top 5 * from tblAssetInfo order by asiDT desc

exec spAssetWatchInit

exec spAssetInfoList
exec spAssetWatchList

exec spAssetRollingPercentSwing 1, 0.010, 5, 'minute',20,0

exec spAssetRollingPercentSwing 1, 0.010, 20, 'minute',10,0
exec spAssetRollingPercentSwing 0, 0.010, 20, 'minute',10,0
exec spAssetRollingPercentSwing 1, 0.010, 20, 'minute',10,1
exec spAssetRollingPercentSwing 0, 0.010, 20, 'minute',10,1
exec spAssetRollingPercentSwing 1, 0.010, 20, 'minute',10,2
exec spAssetRollingPercentSwing 0, 0.010, 20, 'minute',10,2

/*
select * from tblAsset where kaPair = 'MUSD' and kaLastTrade >2.4 order by kaLastTrade desc
select top 100 * from tblAsset where kaPair = 'MUSD'  order by kaId desc



-- exec spAssetRestart
select top 1 asiDT from tblAssetInfo order by asiDT desc
select COUNT (*) from tblAssetInfo
exec spKrakenRollingPercentSwingTest 0.010, 5, 'minute',0
exec spKrakenRollingPercentSwingTest 0.010, 5, 'minute',1
exec spKrakenRollingPercentSwingTest 0.010, 5, 'minute',2
select top 1 asiId from tblAssetInfo order by asiDT desc

exec spKrakenRollingPercentSwing 0.010, 5, 'minute',0
exec spKrakenRollingPercentSwing 0.010, 5, 'minute',1



exec spKrakenRollingPercentSwing 0.010, 5, 'minute',2
exec spKrakenRollingPercentSwing 0.010, 5, 'minute',3

select * from tblAsset where kaPair like '%dog%'
exec spKrakenRollingPercentSwing 0.010, 10, 'minute',1
exec spKrakenRollingPercentSwing 0.010, 60, 'minute',0
exec spKrakenRollingPercentSwing 0.010, 60, 'minute',1

EXEC sp_helpindex 'tblAsset';
EXEC sp_helpindex 'tblAssetInfo';
EXEC sp_who2;

*/


-- exec spKrakenAssetInfoNextId



-- select top 1 max(kaLastTrade) as maxKA,min(kaLastTrade) as minKA from tblAsset where kaPair='CROUSD' group by kaPair

-- exec spKrakenBiggestChangePer5Min



