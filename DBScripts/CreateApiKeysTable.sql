USE [C:\USERS\SHRAVAN\DOCUMENTS\PSEUDO MARKETS\PMUNIFIEDAPI\PSEUDOMARKETSDB.MDF]
GO

/****** Object:  Table [dbo].[ApiKeys]    Script Date: 4/14/2020 3:51:58 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[ApiKeys](
	[Id] [int] NOT NULL,
	[ProviderName] [varchar](50) NOT NULL,
	[ApiKey] [nvarchar](max) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO

