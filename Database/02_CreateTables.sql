-- Core tables for LibraryManagementDB (run after 01_CreateDatabase.sql).

USE [LibraryManagementDB];
GO

IF OBJECT_ID(N'dbo.Reservations', N'U') IS NOT NULL DROP TABLE dbo.Reservations;
IF OBJECT_ID(N'dbo.LoanRenewals', N'U') IS NOT NULL DROP TABLE dbo.LoanRenewals;
IF OBJECT_ID(N'dbo.Loans', N'U') IS NOT NULL DROP TABLE dbo.Loans;
IF OBJECT_ID(N'dbo.Members', N'U') IS NOT NULL DROP TABLE dbo.Members;
IF OBJECT_ID(N'dbo.BookCopies', N'U') IS NOT NULL DROP TABLE dbo.BookCopies;
IF OBJECT_ID(N'dbo.BookAuthors', N'U') IS NOT NULL DROP TABLE dbo.BookAuthors;
IF OBJECT_ID(N'dbo.Books', N'U') IS NOT NULL DROP TABLE dbo.Books;
IF OBJECT_ID(N'dbo.Authors', N'U') IS NOT NULL DROP TABLE dbo.Authors;
IF OBJECT_ID(N'dbo.Publishers', N'U') IS NOT NULL DROP TABLE dbo.Publishers;
IF OBJECT_ID(N'dbo.Categories', N'U') IS NOT NULL DROP TABLE dbo.Categories;
GO

CREATE TABLE dbo.Categories
(
    CategoryId   INT IDENTITY(1,1) NOT NULL,
    CategoryName NVARCHAR(100)     NOT NULL,
    Description  NVARCHAR(500)     NULL,
    CreatedAt    DATETIME2(0)      NOT NULL CONSTRAINT DF_Categories_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Categories PRIMARY KEY CLUSTERED (CategoryId)
);
GO

CREATE TABLE dbo.Publishers
(
    PublisherId   INT IDENTITY(1,1) NOT NULL,
    PublisherName NVARCHAR(200)     NOT NULL,
    ContactEmail  NVARCHAR(150)     NULL,
    Phone         NVARCHAR(30)      NULL,
    Address       NVARCHAR(500)     NULL,
    CreatedAt     DATETIME2(0)      NOT NULL CONSTRAINT DF_Publishers_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Publishers PRIMARY KEY CLUSTERED (PublisherId)
);
GO

CREATE TABLE dbo.Authors
(
    AuthorId   INT IDENTITY(1,1) NOT NULL,
    AuthorName NVARCHAR(200)     NOT NULL,
    Biography  NVARCHAR(MAX)     NULL,
    CreatedAt  DATETIME2(0)      NOT NULL CONSTRAINT DF_Authors_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Authors PRIMARY KEY CLUSTERED (AuthorId)
);
GO

CREATE TABLE dbo.Books
(
    BookId          INT IDENTITY(1,1) NOT NULL,
    Title           NVARCHAR(300)     NOT NULL,
    ISBN            NVARCHAR(20)      NOT NULL,
    CategoryId      INT               NOT NULL,
    PublisherId     INT               NULL,
    PublicationYear INT               NOT NULL,
    Edition         NVARCHAR(50)      NULL,
    Language        NVARCHAR(50)      NOT NULL CONSTRAINT DF_Books_Language DEFAULT N'English',
    Description     NVARCHAR(MAX)     NULL,
    ShelfLocation   NVARCHAR(50)      NULL,
    CreatedAt       DATETIME2(0)      NOT NULL CONSTRAINT DF_Books_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt       DATETIME2(0)      NOT NULL CONSTRAINT DF_Books_UpdatedAt DEFAULT SYSUTCDATETIME(),
    IsActive        BIT               NOT NULL CONSTRAINT DF_Books_IsActive DEFAULT (1),
    CONSTRAINT PK_Books PRIMARY KEY CLUSTERED (BookId)
);
GO

CREATE TABLE dbo.BookAuthors
(
    BookId   INT NOT NULL,
    AuthorId INT NOT NULL,
    CONSTRAINT PK_BookAuthors PRIMARY KEY CLUSTERED (BookId, AuthorId)
);
GO

CREATE TABLE dbo.BookCopies
(
    CopyId          INT IDENTITY(1,1) NOT NULL,
    BookId          INT               NOT NULL,
    AccessionNumber NVARCHAR(50)      NOT NULL,
    ShelfLocation   NVARCHAR(50)      NULL,
    AcquisitionDate DATETIME2(0)      NOT NULL CONSTRAINT DF_BookCopies_AcquisitionDate DEFAULT SYSUTCDATETIME(),
    CopyStatus      NVARCHAR(20)      NOT NULL CONSTRAINT DF_BookCopies_CopyStatus DEFAULT N'Available',
    Notes           NVARCHAR(500)     NULL,
    CreatedAt       DATETIME2(0)      NOT NULL CONSTRAINT DF_BookCopies_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_BookCopies PRIMARY KEY CLUSTERED (CopyId)
);
GO

CREATE TABLE dbo.Members
(
    MemberId             INT IDENTITY(1,1) NOT NULL,
    MembershipNumber     NVARCHAR(50)      NOT NULL,
    FullName             NVARCHAR(200)     NOT NULL,
    Email                NVARCHAR(150)     NOT NULL,
    Phone                NVARCHAR(30)      NOT NULL,
    Address              NVARCHAR(500)     NULL,
    RegistrationDate     DATETIME2(0)      NOT NULL CONSTRAINT DF_Members_RegistrationDate DEFAULT SYSUTCDATETIME(),
    MembershipExpiryDate DATETIME2(0)      NOT NULL,
    MembershipStatus     NVARCHAR(20)      NOT NULL CONSTRAINT DF_Members_MembershipStatus DEFAULT N'Active',
    CreatedAt            DATETIME2(0)      NOT NULL CONSTRAINT DF_Members_CreatedAt DEFAULT SYSUTCDATETIME(),
    UpdatedAt            DATETIME2(0)      NOT NULL CONSTRAINT DF_Members_UpdatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Members PRIMARY KEY CLUSTERED (MemberId)
);
GO

CREATE TABLE dbo.Loans
(
    LoanId       INT IDENTITY(1,1) NOT NULL,
    MemberId     INT               NOT NULL,
    CopyId       INT               NOT NULL,
    IssueDate    DATETIME2(0)      NOT NULL CONSTRAINT DF_Loans_IssueDate DEFAULT SYSUTCDATETIME(),
    DueDate      DATETIME2(0)      NOT NULL,
    ReturnDate   DATETIME2(0)      NULL,
    LoanStatus   NVARCHAR(20)      NOT NULL CONSTRAINT DF_Loans_LoanStatus DEFAULT N'Issued',
    FineAmount   DECIMAL(18,2)     NOT NULL CONSTRAINT DF_Loans_FineAmount DEFAULT (0),
    RenewalCount INT               NOT NULL CONSTRAINT DF_Loans_RenewalCount DEFAULT (0),
    Notes        NVARCHAR(500)     NULL,
    CreatedAt    DATETIME2(0)      NOT NULL CONSTRAINT DF_Loans_CreatedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_Loans PRIMARY KEY CLUSTERED (LoanId)
);
GO

CREATE TABLE dbo.LoanRenewals
(
    RenewalId       INT IDENTITY(1,1) NOT NULL,
    LoanId          INT               NOT NULL,
    PreviousDueDate DATETIME2(0)      NOT NULL,
    NewDueDate      DATETIME2(0)      NOT NULL,
    RenewalDate     DATETIME2(0)      NOT NULL CONSTRAINT DF_LoanRenewals_RenewalDate DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_LoanRenewals PRIMARY KEY CLUSTERED (RenewalId)
);
GO

CREATE TABLE dbo.Reservations
(
    ReservationId     INT IDENTITY(1,1) NOT NULL,
    MemberId          INT               NOT NULL,
    BookId            INT               NOT NULL,
    ReservationDate   DATETIME2(0)      NOT NULL CONSTRAINT DF_Reservations_ReservationDate DEFAULT SYSUTCDATETIME(),
    ReservationStatus NVARCHAR(20)      NOT NULL CONSTRAINT DF_Reservations_Status DEFAULT N'Pending',
    FulfilledDate     DATETIME2(0)      NULL,
    CONSTRAINT PK_Reservations PRIMARY KEY CLUSTERED (ReservationId)
);
GO
