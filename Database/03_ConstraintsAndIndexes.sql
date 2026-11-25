-- Foreign keys, unique constraints, check constraints, and indexes (run after 02_CreateTables.sql).

USE [LibraryManagementDB];
GO

-- Unique constraints
ALTER TABLE dbo.Categories ADD CONSTRAINT UQ_Categories_CategoryName UNIQUE (CategoryName);
ALTER TABLE dbo.Publishers ADD CONSTRAINT UQ_Publishers_PublisherName UNIQUE (PublisherName);
ALTER TABLE dbo.Books ADD CONSTRAINT UQ_Books_ISBN UNIQUE (ISBN);
ALTER TABLE dbo.BookCopies ADD CONSTRAINT UQ_BookCopies_AccessionNumber UNIQUE (AccessionNumber);
ALTER TABLE dbo.Members ADD CONSTRAINT UQ_Members_MembershipNumber UNIQUE (MembershipNumber);
ALTER TABLE dbo.Members ADD CONSTRAINT UQ_Members_Email UNIQUE (Email);
GO

-- Foreign keys
ALTER TABLE dbo.Books
    ADD CONSTRAINT FK_Books_Categories FOREIGN KEY (CategoryId) REFERENCES dbo.Categories (CategoryId) ON DELETE NO ACTION,
        CONSTRAINT FK_Books_Publishers FOREIGN KEY (PublisherId) REFERENCES dbo.Publishers (PublisherId) ON DELETE NO ACTION;

ALTER TABLE dbo.BookAuthors
    ADD CONSTRAINT FK_BookAuthors_Books FOREIGN KEY (BookId) REFERENCES dbo.Books (BookId) ON DELETE CASCADE,
        CONSTRAINT FK_BookAuthors_Authors FOREIGN KEY (AuthorId) REFERENCES dbo.Authors (AuthorId) ON DELETE NO ACTION;

ALTER TABLE dbo.BookCopies
    ADD CONSTRAINT FK_BookCopies_Books FOREIGN KEY (BookId) REFERENCES dbo.Books (BookId) ON DELETE NO ACTION;

ALTER TABLE dbo.Loans
    ADD CONSTRAINT FK_Loans_Members FOREIGN KEY (MemberId) REFERENCES dbo.Members (MemberId) ON DELETE NO ACTION,
        CONSTRAINT FK_Loans_BookCopies FOREIGN KEY (CopyId) REFERENCES dbo.BookCopies (CopyId) ON DELETE NO ACTION;

ALTER TABLE dbo.LoanRenewals
    ADD CONSTRAINT FK_LoanRenewals_Loans FOREIGN KEY (LoanId) REFERENCES dbo.Loans (LoanId) ON DELETE CASCADE;

ALTER TABLE dbo.Reservations
    ADD CONSTRAINT FK_Reservations_Members FOREIGN KEY (MemberId) REFERENCES dbo.Members (MemberId) ON DELETE NO ACTION,
        CONSTRAINT FK_Reservations_Books FOREIGN KEY (BookId) REFERENCES dbo.Books (BookId) ON DELETE NO ACTION;
GO

-- Check constraints
ALTER TABLE dbo.Books
    ADD CONSTRAINT CK_Books_PublicationYear CHECK (PublicationYear BETWEEN 1450 AND 2100);

ALTER TABLE dbo.BookCopies
    ADD CONSTRAINT CK_BookCopies_Status CHECK (CopyStatus IN (N'Available', N'Borrowed', N'Reserved', N'Lost', N'Damaged', N'Inactive'));

ALTER TABLE dbo.Members
    ADD CONSTRAINT CK_Members_Status CHECK (MembershipStatus IN (N'Active', N'Inactive', N'Expired'));

ALTER TABLE dbo.Loans
    ADD CONSTRAINT CK_Loans_Status CHECK (LoanStatus IN (N'Issued', N'Returned', N'Overdue')),
        CONSTRAINT CK_Loans_FineAmount_NonNegative CHECK (FineAmount >= 0),
        CONSTRAINT CK_Loans_RenewalCount_NonNegative CHECK (RenewalCount >= 0),
        CONSTRAINT CK_Loans_DueAfterIssue CHECK (DueDate >= IssueDate);

ALTER TABLE dbo.Reservations
    ADD CONSTRAINT CK_Reservations_Status CHECK (ReservationStatus IN (N'Pending', N'Fulfilled', N'Cancelled'));
GO

-- Prevent two simultaneous unreturned loans on the same physical copy
CREATE UNIQUE NONCLUSTERED INDEX UX_Loans_ActiveCopy
    ON dbo.Loans (CopyId)
    WHERE ReturnDate IS NULL;

-- Query indexes
CREATE NONCLUSTERED INDEX IX_Books_Title ON dbo.Books (Title);
CREATE NONCLUSTERED INDEX IX_Books_CategoryId ON dbo.Books (CategoryId);
CREATE NONCLUSTERED INDEX IX_Books_PublisherId ON dbo.Books (PublisherId) WHERE PublisherId IS NOT NULL;
CREATE NONCLUSTERED INDEX IX_BookCopies_BookId_Status ON dbo.BookCopies (BookId, CopyStatus);
CREATE NONCLUSTERED INDEX IX_Members_Status ON dbo.Members (MembershipStatus);
CREATE NONCLUSTERED INDEX IX_Loans_MemberId_Status ON dbo.Loans (MemberId, LoanStatus) INCLUDE (CopyId, DueDate, ReturnDate, FineAmount);
CREATE NONCLUSTERED INDEX IX_Loans_DueDate ON dbo.Loans (DueDate) WHERE ReturnDate IS NULL;
CREATE NONCLUSTERED INDEX IX_Loans_IssueDate ON dbo.Loans (IssueDate DESC) INCLUDE (MemberId, CopyId, LoanStatus);
CREATE NONCLUSTERED INDEX IX_Reservations_BookId_Status ON dbo.Reservations (BookId, ReservationStatus);
GO
