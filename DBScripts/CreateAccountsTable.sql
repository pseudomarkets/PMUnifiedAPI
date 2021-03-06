USE PseudoMarketsDB
GO

/****** Object:  Table [dbo].[Accounts]    Script Date: 4/14/2020 3:51:47 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[Accounts](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[UserID] [int] NOT NULL,
	[Balance] [float] NOT NULL,
 CONSTRAINT [PK__Accounts__3214EC07CD91311B] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
) ON [PRIMARY]
GO

ALTER TABLE [dbo].[Accounts] ADD  CONSTRAINT [DF__Accounts__Balanc__31EC6D26]  DEFAULT ((1000000.00)) FOR [Balance]
GO

ALTER TABLE [dbo].[Accounts]  WITH CHECK ADD  CONSTRAINT [FK_Accounts_Users] FOREIGN KEY([UserID])
REFERENCES [dbo].[Users] ([Id])
GO

ALTER TABLE [dbo].[Accounts] CHECK CONSTRAINT [FK_Accounts_Users]
GO

