-- Script Date: 9/1/2020 8:27 PM  - ErikEJ.SqlCeScripting version 3.5.2.87
SELECT 1;
PRAGMA foreign_keys=OFF;
BEGIN TRANSACTION;
CREATE TABLE [Users] (
  [Id] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
, [username] nvarchar(50) NOT NULL COLLATE NOCASE
, [password] nvarchar(50) NOT NULL COLLATE NOCASE
, [Salt] image NOT NULL
);
CREATE TABLE [Transactions] (
  [Id] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
, [AccountId] int NOT NULL
, [TransactionId] nvarchar(36) NOT NULL COLLATE NOCASE
);
CREATE TABLE [Tokens] (
  [Id] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
, [UserID] int NOT NULL
, [Token] ntext NOT NULL
, CONSTRAINT [FK_Tokens_Users] FOREIGN KEY ([UserID]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION ON UPDATE NO ACTION
);
CREATE TABLE [sysdiagrams] (
  [name] nvarchar(128) NOT NULL
, [principal_id] int NOT NULL
, [diagram_id] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
, [version] int NULL
, [definition] image NULL
);
CREATE TABLE [StockSplits] (
  [Id] INTEGER NOT NULL
, [Symbol] nvarchar(10) NOT NULL COLLATE NOCASE
, [Ratio] int NOT NULL
, [ExDate] datetime NOT NULL
);
CREATE TABLE [Orders] (
  [Id] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
, [Symbol] nvarchar(20) NOT NULL COLLATE NOCASE
, [Type] nvarchar(10) NOT NULL COLLATE NOCASE
, [Price] float NOT NULL
, [Quantity] int NOT NULL
, [Date] datetime NOT NULL
, [TransactionID] nvarchar(36) NOT NULL COLLATE NOCASE
);
CREATE TABLE [ApiKeys] (
  [Id] int NOT NULL
, [ProviderName] nvarchar(50) NOT NULL COLLATE NOCASE
, [ApiKey] ntext NOT NULL
, CONSTRAINT [PK__ApiKeys__3214EC07F518960C] PRIMARY KEY ([Id])
);
CREATE TABLE [Accounts] (
  [Id] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
, [UserID] int NOT NULL
, [Balance] float DEFAULT (1000000.00) NOT NULL
, CONSTRAINT [FK_Accounts_Users] FOREIGN KEY ([UserID]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION ON UPDATE NO ACTION
);
CREATE TABLE [Positions] (
  [Id] INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL
, [AccountId] int NOT NULL
, [OrderId] int NOT NULL
, [Value] float NOT NULL
, [Symbol] nvarchar(20) NOT NULL COLLATE NOCASE
, [Quantity] int NOT NULL
, CONSTRAINT [FK_Positions_Accounts] FOREIGN KEY ([AccountId]) REFERENCES [Accounts] ([Id]) ON DELETE NO ACTION ON UPDATE NO ACTION
);
CREATE INDEX [Users_IX_Users] ON [Users] ([Id] ASC);
CREATE INDEX [Transactions_IX_Transactions] ON [Transactions] ([Id] ASC);
CREATE INDEX [Transactions_IX_Transactions_1] ON [Transactions] ([AccountId] ASC);
CREATE UNIQUE INDEX [sysdiagrams_UK_principal_name] ON [sysdiagrams] ([principal_id] ASC,[name] ASC);
CREATE INDEX [Orders_IX_Orders] ON [Orders] ([Id] ASC);
CREATE INDEX [ApiKeys_IX_ApiKeys] ON [ApiKeys] ([Id] ASC);
CREATE INDEX [Accounts_IX_Accounts] ON [Accounts] ([Id] ASC,[UserID] ASC);
CREATE TRIGGER [fki_Tokens_UserID_Users_Id] BEFORE Insert ON [Tokens] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Insert on table Tokens violates foreign key constraint FK_Tokens_Users') WHERE (SELECT Id FROM Users WHERE  Id = NEW.UserID) IS NULL; END;
CREATE TRIGGER [fku_Tokens_UserID_Users_Id] BEFORE Update ON [Tokens] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Update on table Tokens violates foreign key constraint FK_Tokens_Users') WHERE (SELECT Id FROM Users WHERE  Id = NEW.UserID) IS NULL; END;
CREATE TRIGGER [fki_Accounts_UserID_Users_Id] BEFORE Insert ON [Accounts] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Insert on table Accounts violates foreign key constraint FK_Accounts_Users') WHERE (SELECT Id FROM Users WHERE  Id = NEW.UserID) IS NULL; END;
CREATE TRIGGER [fku_Accounts_UserID_Users_Id] BEFORE Update ON [Accounts] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Update on table Accounts violates foreign key constraint FK_Accounts_Users') WHERE (SELECT Id FROM Users WHERE  Id = NEW.UserID) IS NULL; END;
CREATE TRIGGER [fki_Positions_AccountId_Accounts_Id] BEFORE Insert ON [Positions] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Insert on table Positions violates foreign key constraint FK_Positions_Accounts') WHERE (SELECT Id FROM Accounts WHERE  Id = NEW.AccountId) IS NULL; END;
CREATE TRIGGER [fku_Positions_AccountId_Accounts_Id] BEFORE Update ON [Positions] FOR EACH ROW BEGIN SELECT RAISE(ROLLBACK, 'Update on table Positions violates foreign key constraint FK_Positions_Accounts') WHERE (SELECT Id FROM Accounts WHERE  Id = NEW.AccountId) IS NULL; END;
COMMIT;

