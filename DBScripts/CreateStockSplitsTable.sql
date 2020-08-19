USE [PseudoMarketsDB]
GO

/****** Object:  Table [dbo].[StockSplits]    Script Date: 8/18/2020 11:31:37 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[StockSplits](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Symbol] [varchar](10) NOT NULL,
	[Ratio] [int] NOT NULL,
	[ExDate] [date] NOT NULL
) ON [PRIMARY]
GO
