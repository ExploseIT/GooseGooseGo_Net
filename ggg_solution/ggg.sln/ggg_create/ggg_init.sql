
USE ggg_db
GO

/****** Object:  Table [dbo].[tblSettings]    Script Date: 11/06/2025 23:06:30 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
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
/****** Object:  View [dbo].[vwSettings]    Script Date: 11/06/2025 23:06:30 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE View [dbo].[vwSettings]
as
SELECT
setId
, setName
, setValue
, setDescription
FROM            dbo.tblSettings
GO

/****** Object:  StoredProcedure [dbo].[spSettingsList]    Script Date: 11/06/2025 23:06:33 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

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
/****** Object:  StoredProcedure [dbo].[spSettingsListAll]    Script Date: 11/06/2025 23:06:33 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

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
/****** Object:  StoredProcedure [dbo].[spSettingsUpdateById]    Script Date: 11/06/2025 23:06:33 ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

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
