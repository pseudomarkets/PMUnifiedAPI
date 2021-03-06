USE PseudoMarketsDB
GO

/****** Object:  Table [dbo].[Positions]    Script Date: 4/14/2020 3:52:39 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Positions](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[AccountId] [int] NOT NULL,
	[OrderId] [int] NOT NULL,
	[Value] [float] NOT NULL,
	[Symbol] [varchar](20) NOT NULL,
	[Quantity] [int] NOT NULL,
 CONSTRAINT [PK__Position__3214EC072FAF3D2D] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[Positions]  WITH CHECK ADD  CONSTRAINT [FK_Positions_Accounts] FOREIGN KEY([AccountId])
REFERENCES [dbo].[Accounts] ([Id])
GO

ALTER TABLE [dbo].[Positions] CHECK CONSTRAINT [FK_Positions_Accounts]
GO

