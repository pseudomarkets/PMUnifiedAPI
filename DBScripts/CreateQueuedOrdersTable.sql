USE PseudoMarketsDB
GO

/****** Object:  Table [dbo].[QueuedOrders]    Script Date: 11/5/2020 9:51:23 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[QueuedOrders](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [int] NOT NULL,
	[OrderDate] [date] NOT NULL,
	[Symbol] [varchar](5) NOT NULL,
	[Quantity] [int] NOT NULL,
	[OrderType] [varchar](9) NOT NULL,
	[IsOpenOrder] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO