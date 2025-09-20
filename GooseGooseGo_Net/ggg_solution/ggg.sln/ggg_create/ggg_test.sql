
use ggg_db
go

/*
select COUNT (*) from tblKrakenAsset
select COUNT (*) from tblKrakenAssetInfo
select top 5 * from tblKrakenAsset order by kaRetrievedAt desc
select top 200 * from tblKrakenAssetInfo order by kaiDT desc

*/

select * from tblKrakenAsset where kaPair = 'MUSD' and kaLastTrade >2.4 order by kaLastTrade desc
select top 100 * from tblKrakenAsset where kaPair = 'MUSD'  order by kaId desc





exec spKrakenRollingPercentSwing 0.010, 10, 'minute'
exec spKrakenRollingPercentSwing 0.010, 60, 'minute'

EXEC sp_helpindex 'tblKrakenAsset';
EXEC sp_helpindex 'tblKrakenAssetInfo';
EXEC sp_who2;
-- exec spKrakenRestart

-- exec spKrakenAssetInfoNextId



-- select top 1 max(kaLastTrade) as maxKA,min(kaLastTrade) as minKA from tblKrakenAsset where kaPair='CROUSD' group by kaPair

-- exec spKrakenBiggestChangePer5Min



