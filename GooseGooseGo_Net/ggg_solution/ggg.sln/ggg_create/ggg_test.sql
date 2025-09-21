
use ggg_db
go

/*
select COUNT (*) from tblKrakenAsset
select COUNT (*) from tblKrakenAssetInfo
select top 5 * from tblKrakenAsset order by kaRetrievedAt desc
select top 200 * from tblKrakenAssetInfo order by kaiDT desc

*/

--select * from tblKrakenAsset where kaPair='MUSD' and kaIndex=(select top 1 kaiId from tblKrakenAssetInfo order by kaiDT desc)
exec spAssetWatchList
exec spKrakenRollingPercentSwing 0.010, 5, 'minute',0
exec spKrakenRollingPercentSwing 0.010, 5, 'minute',1
exec spKrakenRollingPercentSwing 0.010, 5, 'minute',2


/*
select * from tblKrakenAsset where kaPair = 'MUSD' and kaLastTrade >2.4 order by kaLastTrade desc
select top 100 * from tblKrakenAsset where kaPair = 'MUSD'  order by kaId desc




select top 1 kaiDT from tblKrakenAssetInfo order by kaiDT desc
select COUNT (*) from tblKrakenAssetInfo
exec spKrakenRollingPercentSwingTest 0.010, 5, 'minute',0
exec spKrakenRollingPercentSwingTest 0.010, 5, 'minute',1
exec spKrakenRollingPercentSwingTest 0.010, 5, 'minute',2
select top 1 kaiId from tblKrakenAssetInfo order by kaiDT desc

exec spKrakenRollingPercentSwing 0.010, 5, 'minute',0
exec spKrakenRollingPercentSwing 0.010, 5, 'minute',1



exec spKrakenRollingPercentSwing 0.010, 5, 'minute',2
exec spKrakenRollingPercentSwing 0.010, 5, 'minute',3

select * from tblKrakenAsset where kaPair like '%dog%'
exec spKrakenRollingPercentSwing 0.010, 10, 'minute',1
exec spKrakenRollingPercentSwing 0.010, 60, 'minute',0
exec spKrakenRollingPercentSwing 0.010, 60, 'minute',1

EXEC sp_helpindex 'tblKrakenAsset';
EXEC sp_helpindex 'tblKrakenAssetInfo';
EXEC sp_who2;

*/
-- exec spKrakenRestart

-- exec spKrakenAssetInfoNextId



-- select top 1 max(kaLastTrade) as maxKA,min(kaLastTrade) as minKA from tblKrakenAsset where kaPair='CROUSD' group by kaPair

-- exec spKrakenBiggestChangePer5Min



