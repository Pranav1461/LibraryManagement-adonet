-- Sample dataset for LibraryManagementDB (run after 03_ConstraintsAndIndexes.sql).

USE [LibraryManagementDB];
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.Categories)
BEGIN
    DECLARE @Now DATETIME2(0) = SYSUTCDATETIME();

    -- Categories (8)
    SET IDENTITY_INSERT dbo.Categories ON;
    INSERT INTO dbo.Categories (CategoryId, CategoryName, Description, CreatedAt)
    VALUES
        (1, N'Fiction',    N'Literary and contemporary fiction works.',                 DATEADD(DAY, -180, @Now)),
        (2, N'Science',    N'Physics, astronomy, biology, and general science.',        DATEADD(DAY, -180, @Now)),
        (3, N'Technology', N'Software engineering, databases, and systems design.',     DATEADD(DAY, -180, @Now)),
        (4, N'History',    N'World history, civilizations, and historical analysis.',   DATEADD(DAY, -180, @Now)),
        (5, N'Biography',  N'Biographies and memoirs of notable figures.',              DATEADD(DAY, -180, @Now)),
        (6, N'Education',  N'Academic textbooks, study guides, and pedagogy.',          DATEADD(DAY, -180, @Now)),
        (7, N'Mystery',    N'Crime, detective fiction, and suspense thrillers.',        DATEADD(DAY, -180, @Now)),
        (8, N'Fantasy',    N'Speculative worlds, epic fantasy, and folklore.',          DATEADD(DAY, -180, @Now));
    SET IDENTITY_INSERT dbo.Categories OFF;

    -- Publishers (5)
    SET IDENTITY_INSERT dbo.Publishers ON;
    INSERT INTO dbo.Publishers (PublisherId, PublisherName, ContactEmail, Phone, Address, CreatedAt)
    VALUES
        (1, N'O''Reilly Media',          N'orders@oreilly.com',      N'+1-707-827-7000', N'1005 Gravenstein Hwy N, Sebastopol, CA', DATEADD(DAY, -180, @Now)),
        (2, N'Pearson Education',        N'support@pearson.com',     N'+44-20-7010-2000',N'80 Strand, London, UK',                  DATEADD(DAY, -180, @Now)),
        (3, N'Penguin Random House',     N'desk@penguinrandom.com',  N'+1-212-782-9000', N'1745 Broadway, New York, NY',            DATEADD(DAY, -180, @Now)),
        (4, N'HarperCollins Publishers', N'library@harpercollins.com',N'+1-212-207-7000',N'195 Broadway, New York, NY',             DATEADD(DAY, -180, @Now)),
        (5, N'Oxford University Press',  N'academic@oup.com',        N'+44-1865-556767', N'Great Clarendon St, Oxford, UK',         DATEADD(DAY, -180, @Now));
    SET IDENTITY_INSERT dbo.Publishers OFF;

    -- Authors (10)
    SET IDENTITY_INSERT dbo.Authors ON;
    INSERT INTO dbo.Authors (AuthorId, AuthorName, Biography, CreatedAt)
    VALUES
        ( 1, N'Robert C. Martin',   N'Software engineer and author focused on clean code and agile design.', DATEADD(DAY, -170, @Now)),
        ( 2, N'Andrew Hunt',        N'Co-author of The Pragmatic Programmer and agile manifesto signatory.',  DATEADD(DAY, -170, @Now)),
        ( 3, N'Martin Fowler',      N'Author and speaker specializing in enterprise software architecture.',  DATEADD(DAY, -170, @Now)),
        ( 4, N'Jon Skeet',          N'C# language specialist and author of C# in Depth.',                   DATEADD(DAY, -170, @Now)),
        ( 5, N'Yuval Noah Harari',  N'Historian and author of Sapiens and Homo Deus.',                      DATEADD(DAY, -170, @Now)),
        ( 6, N'Carl Sagan',         N'Astronomer, planetary scientist, and science communicator.',          DATEADD(DAY, -170, @Now)),
        ( 7, N'Walter Isaacson',    N'Biographer of Steve Jobs, Albert Einstein, and Leonardo da Vinci.',   DATEADD(DAY, -170, @Now)),
        ( 8, N'Agatha Christie',    N'Renowned mystery novelist known for Poirot and Miss Marple.',         DATEADD(DAY, -170, @Now)),
        ( 9, N'Brandon Sanderson',  N'Fantasy author of Mistborn and The Stormlight Archive.',              DATEADD(DAY, -170, @Now)),
        (10, N'George Orwell',      N'Novelist and essayist famous for 1984 and Animal Farm.',              DATEADD(DAY, -170, @Now));
    SET IDENTITY_INSERT dbo.Authors OFF;

    -- Books (25)
    SET IDENTITY_INSERT dbo.Books ON;
    INSERT INTO dbo.Books
        (BookId, Title, ISBN, CategoryId, PublisherId, PublicationYear, Edition, Language, Description, ShelfLocation, CreatedAt, UpdatedAt, IsActive)
    VALUES
        ( 1, N'Clean Code',                               '9780132350884', 3, 2, 2008, N'1st',  N'English', N'A handbook of agile software craftsmanship.',                      N'T-101', DATEADD(DAY, -160, @Now), DATEADD(DAY, -160, @Now), 1),
        ( 2, N'Clean Architecture',                       '9780134494166', 3, 2, 2017, N'1st',  N'English', N'A craftsman''s guide to software structure and design.',            N'T-102', DATEADD(DAY, -160, @Now), DATEADD(DAY, -160, @Now), 1),
        ( 3, N'The Pragmatic Programmer',                 '9780135957059', 3, 2, 2019, N'20th', N'English', N'Practical lessons for building maintainable software.',             N'T-103', DATEADD(DAY, -160, @Now), DATEADD(DAY, -160, @Now), 1),
        ( 4, N'Refactoring',                              '9780134757599', 3, 2, 2018, N'2nd',  N'English', N'Improving the design of existing code.',                            N'T-104', DATEADD(DAY, -160, @Now), DATEADD(DAY, -160, @Now), 1),
        ( 5, N'Patterns of Enterprise Application Arch.', '9780321127426', 3, 2, 2002, N'1st',  N'English', N'Classic patterns for layered enterprise systems.',                  N'T-105', DATEADD(DAY, -160, @Now), DATEADD(DAY, -160, @Now), 1),
        ( 6, N'C# in Depth',                              '9781617294532', 3, 1, 2019, N'4th',  N'English', N'Deep dive into C# language features and evolution.',                N'T-106', DATEADD(DAY, -160, @Now), DATEADD(DAY, -160, @Now), 1),
        ( 7, N'Sapiens: A Brief History of Humankind',    '9780062316097', 4, 4, 2015, N'1st',  N'English', N'Survey of human history from the Stone Age to today.',              N'H-201', DATEADD(DAY, -155, @Now), DATEADD(DAY, -155, @Now), 1),
        ( 8, N'Homo Deus',                                '9780062464316', 4, 4, 2017, N'1st',  N'English', N'An examination of humanity''s future challenges.',                  N'H-202', DATEADD(DAY, -155, @Now), DATEADD(DAY, -155, @Now), 1),
        ( 9, N'21 Lessons for the 21st Century',          '9780525512172', 6, 3, 2018, N'1st',  N'English', N'Reflections on technology, politics, and modern society.',          N'E-301', DATEADD(DAY, -155, @Now), DATEADD(DAY, -155, @Now), 1),
        (10, N'Cosmos',                                   '9780345331359', 2, 3, 1980, N'1st',  N'English', N'Exploration of cosmic evolution and scientific discovery.',           N'S-101', DATEADD(DAY, -150, @Now), DATEADD(DAY, -150, @Now), 1),
        (11, N'Pale Blue Dot',                            '9780345376596', 2, 3, 1994, N'1st',  N'English', N'A vision of the human future in space.',                            N'S-102', DATEADD(DAY, -150, @Now), DATEADD(DAY, -150, @Now), 1),
        (12, N'The Demon-Haunted World',                  '9780345409461', 2, 3, 1996, N'1st',  N'English', N'Science as a candle in the dark.',                                  N'S-103', DATEADD(DAY, -150, @Now), DATEADD(DAY, -150, @Now), 1),
        (13, N'Steve Jobs',                               '9781451648539', 5, 3, 2011, N'1st',  N'English', N'Authorized biography based on over forty interviews.',              N'B-101', DATEADD(DAY, -145, @Now), DATEADD(DAY, -145, @Now), 1),
        (14, N'Einstein: His Life and Universe',          '9780743264747', 5, 3, 2007, N'1st',  N'English', N'Biography examining Einstein''s science and personal life.',        N'B-102', DATEADD(DAY, -145, @Now), DATEADD(DAY, -145, @Now), 1),
        (15, N'Leonardo da Vinci',                        '9781501139154', 5, 3, 2017, N'1st',  N'English', N'Portrait of the Renaissance polymath.',                             N'B-103', DATEADD(DAY, -145, @Now), DATEADD(DAY, -145, @Now), 1),
        (16, N'The Innovators',                           '9781476708690', 4, 3, 2014, N'1st',  N'English', N'History of the pioneers behind the digital revolution.',            N'H-203', DATEADD(DAY, -145, @Now), DATEADD(DAY, -145, @Now), 1),
        (17, N'Murder on the Orient Express',             '9780062693662', 7, 4, 1934, N'Rev',  N'English', N'Hercule Poirot investigates a murder aboard a snowbound train.',    N'M-101', DATEADD(DAY, -140, @Now), DATEADD(DAY, -140, @Now), 1),
        (18, N'And Then There Were None',                 '9780062073488', 7, 4, 1939, N'Rev',  N'English', N'Ten strangers invited to an isolated island face a grim reckoning.',  N'M-102', DATEADD(DAY, -140, @Now), DATEADD(DAY, -140, @Now), 1),
        (19, N'The Murder of Roger Ackroyd',              '9780062073563', 7, 4, 1926, N'Rev',  N'English', N'Classic Poirot mystery in a quiet English village.',                N'M-103', DATEADD(DAY, -140, @Now), DATEADD(DAY, -140, @Now), 1),
        (20, N'The Way of Kings',                         '9780765326355', 8, 4, 2010, N'1st',  N'English', N'First volume of The Stormlight Archive.',                           N'FN-101',DATEADD(DAY, -135, @Now), DATEADD(DAY, -135, @Now), 1),
        (21, N'Mistborn: The Final Empire',               '9780765311788', 8, 4, 2006, N'1st',  N'English', N'A heist fantasy set in an ash-covered empire.',                     N'FN-102',DATEADD(DAY, -135, @Now), DATEADD(DAY, -135, @Now), 1),
        (22, N'1984',                                     '9780451524935', 1, 3, 1949, N'Cent', N'English', N'Dystopian novel on surveillance and authoritarianism.',               N'F-101', DATEADD(DAY, -130, @Now), DATEADD(DAY, -130, @Now), 1),
        (23, N'Animal Farm',                              '9780451526342', 1, 3, 1945, N'Annv', N'English', N'Political satire told through a farmyard rebellion.',               N'F-102', DATEADD(DAY, -130, @Now), DATEADD(DAY, -130, @Now), 1),
        (24, N'Domain-Driven Design Distilled',           '9780134434421', 6, 2, 2016, N'1st',  N'English', N'Concise guide to bounded contexts and domain modeling.',            N'E-302', DATEADD(DAY, -125, @Now), DATEADD(DAY, -125, @Now), 1),
        (25, N'Agile Principles & Practices',             '9780131857254', 6, 5, 2006, N'1st',  N'English', N'Object-oriented design and agile engineering workbook.',            N'E-303', DATEADD(DAY, -125, @Now), DATEADD(DAY, -125, @Now), 1);
    SET IDENTITY_INSERT dbo.Books OFF;

    -- BookAuthors (includes multi-author collaborations)
    INSERT INTO dbo.BookAuthors (BookId, AuthorId)
    VALUES
        (1, 1),
        (2, 1),
        (3, 2), (3, 3),
        (4, 3),
        (5, 3),
        (6, 4),
        (7, 5),
        (8, 5),
        (9, 5),
        (10, 6),
        (11, 6),
        (12, 6),
        (13, 7),
        (14, 7),
        (15, 7),
        (16, 7), (16, 2),
        (17, 8),
        (18, 8),
        (19, 8),
        (20, 9),
        (21, 9),
        (22, 10),
        (23, 10),
        (24, 3), (24, 1),
        (25, 1), (25, 4);

    -- Members (20: 17 Active, 2 Inactive, 1 Expired)
    SET IDENTITY_INSERT dbo.Members ON;
    INSERT INTO dbo.Members
        (MemberId, MembershipNumber, FullName, Email, Phone, Address, RegistrationDate, MembershipExpiryDate, MembershipStatus, CreatedAt, UpdatedAt)
    VALUES
        ( 1, N'MEM-001', N'Aarav Sharma',    N'aarav.sharma@example.com',   N'+91-98201-10001', N'14 MG Road, Pune',          DATEADD(DAY, -160, @Now), DATEADD(DAY,  205, @Now), N'Active',   DATEADD(DAY, -160, @Now), DATEADD(DAY, -160, @Now)),
        ( 2, N'MEM-002', N'Priya Nair',      N'priya.nair@example.com',     N'+91-98201-10002', N'22 Indiranagar, Bengaluru', DATEADD(DAY, -155, @Now), DATEADD(DAY,  210, @Now), N'Active',   DATEADD(DAY, -155, @Now), DATEADD(DAY, -155, @Now)),
        ( 3, N'MEM-003', N'Rohan Kulkarni',  N'rohan.k@example.com',        N'+91-98201-10003', N'8 Baner Rd, Pune',          DATEADD(DAY, -150, @Now), DATEADD(DAY,  215, @Now), N'Active',   DATEADD(DAY, -150, @Now), DATEADD(DAY, -150, @Now)),
        ( 4, N'MEM-004', N'Meera Iyer',      N'meera.iyer@example.com',     N'+91-98201-10004', N'41 Adyar, Chennai',         DATEADD(DAY, -145, @Now), DATEADD(DAY,  220, @Now), N'Active',   DATEADD(DAY, -145, @Now), DATEADD(DAY, -145, @Now)),
        ( 5, N'MEM-005', N'Vikram Desai',    N'vikram.desai@example.com',   N'+91-98201-10005', N'5 Navrangpura, Ahmedabad',  DATEADD(DAY, -140, @Now), DATEADD(DAY,  225, @Now), N'Active',   DATEADD(DAY, -140, @Now), DATEADD(DAY, -140, @Now)),
        ( 6, N'MEM-006', N'Ananya Bose',     N'ananya.bose@example.com',    N'+91-98201-10006', N'19 Salt Lake, Kolkata',     DATEADD(DAY, -130, @Now), DATEADD(DAY,  235, @Now), N'Active',   DATEADD(DAY, -130, @Now), DATEADD(DAY, -130, @Now)),
        ( 7, N'MEM-007', N'Kabir Mehta',     N'kabir.mehta@example.com',    N'+91-98201-10007', N'77 Bandra West, Mumbai',    DATEADD(DAY, -125, @Now), DATEADD(DAY,  240, @Now), N'Active',   DATEADD(DAY, -125, @Now), DATEADD(DAY, -125, @Now)),
        ( 8, N'MEM-008', N'Sneha Patel',     N'sneha.patel@example.com',    N'+91-98201-10008', N'31 Alkapuri, Vadodara',     DATEADD(DAY, -120, @Now), DATEADD(DAY,  245, @Now), N'Active',   DATEADD(DAY, -120, @Now), DATEADD(DAY, -120, @Now)),
        ( 9, N'MEM-009', N'Arjun Reddy',     N'arjun.reddy@example.com',    N'+91-98201-10009', N'12 Jubilee Hills, Hyd',     DATEADD(DAY, -110, @Now), DATEADD(DAY,  255, @Now), N'Active',   DATEADD(DAY, -110, @Now), DATEADD(DAY, -110, @Now)),
        (10, N'MEM-010', N'Neha Verma',      N'neha.verma@example.com',     N'+91-98201-10010', N'64 Gomti Nagar, Lucknow',   DATEADD(DAY, -100, @Now), DATEADD(DAY,  265, @Now), N'Active',   DATEADD(DAY, -100, @Now), DATEADD(DAY, -100, @Now)),
        (11, N'MEM-011', N'Siddharth Rao',   N'siddharth.rao@example.com',  N'+91-98201-10011', N'9 Malleswaram, Bengaluru',  DATEADD(DAY,  -95, @Now), DATEADD(DAY,  270, @Now), N'Active',   DATEADD(DAY,  -95, @Now), DATEADD(DAY,  -95, @Now)),
        (12, N'MEM-012', N'Kavya Menon',     N'kavya.menon@example.com',    N'+91-98201-10012', N'18 Panampilly, Kochi',      DATEADD(DAY,  -90, @Now), DATEADD(DAY,  275, @Now), N'Active',   DATEADD(DAY,  -90, @Now), DATEADD(DAY,  -90, @Now)),
        (13, N'MEM-013', N'Devansh Joshi',   N'devansh.j@example.com',      N'+91-98201-10013', N'44 Civil Lines, Jaipur',    DATEADD(DAY,  -85, @Now), DATEADD(DAY,  280, @Now), N'Active',   DATEADD(DAY,  -85, @Now), DATEADD(DAY,  -85, @Now)),
        (14, N'MEM-014', N'Ishita Gupta',    N'ishita.gupta@example.com',   N'+91-98201-10014', N'52 Saket, New Delhi',       DATEADD(DAY,  -80, @Now), DATEADD(DAY,  285, @Now), N'Active',   DATEADD(DAY,  -80, @Now), DATEADD(DAY,  -80, @Now)),
        (15, N'MEM-015', N'Aditya Singh',    N'aditya.singh@example.com',   N'+91-98201-10015', N'27 Sector 17, Chandigarh',  DATEADD(DAY,  -70, @Now), DATEADD(DAY,  295, @Now), N'Active',   DATEADD(DAY,  -70, @Now), DATEADD(DAY,  -70, @Now)),
        (16, N'MEM-016', N'Tanvi Kapoor',    N'tanvi.kapoor@example.com',   N'+91-98201-10016', N'15 Koregaon Park, Pune',    DATEADD(DAY,  -60, @Now), DATEADD(DAY,  305, @Now), N'Active',   DATEADD(DAY,  -60, @Now), DATEADD(DAY,  -60, @Now)),
        (17, N'MEM-017', N'Nikhil Bansal',   N'nikhil.bansal@example.com',  N'+91-98201-10017', N'83 Noida Sec 62, UP',       DATEADD(DAY,  -50, @Now), DATEADD(DAY,  315, @Now), N'Active',   DATEADD(DAY,  -50, @Now), DATEADD(DAY,  -50, @Now)),
        (18, N'MEM-018', N'Pooja Chatterjee',N'pooja.c@example.com',        N'+91-98201-10018', N'6 Park Street, Kolkata',    DATEADD(DAY, -200, @Now), DATEADD(DAY,  120, @Now), N'Inactive', DATEADD(DAY, -200, @Now), DATEADD(DAY,  -30, @Now)),
        (19, N'MEM-019', N'Harsh Vardhan',   N'harsh.v@example.com',        N'+91-98201-10019', N'11 Hazratganj, Lucknow',    DATEADD(DAY, -240, @Now), DATEADD(DAY,   60, @Now), N'Inactive', DATEADD(DAY, -240, @Now), DATEADD(DAY,  -15, @Now)),
        (20, N'MEM-020', N'Ritu Malhotra',   N'ritu.malhotra@example.com',  N'+91-98201-10020', N'30 Rajouri Garden, Delhi',  DATEADD(DAY, -400, @Now), DATEADD(DAY,  -35, @Now), N'Expired',  DATEADD(DAY, -400, @Now), DATEADD(DAY,  -35, @Now));
    SET IDENTITY_INSERT dbo.Members OFF;

    -- Physical BookCopies (45)
    -- Copies 2, 5, 9, 14, 18, 22, 27, 31, 35 are currently Borrowed by the 9 unreturned loans below.
    -- Copy 44 is Damaged, Copy 45 is Lost.
    SET IDENTITY_INSERT dbo.BookCopies ON;
    INSERT INTO dbo.BookCopies
        (CopyId, BookId, AccessionNumber, ShelfLocation, AcquisitionDate, CopyStatus, Notes, CreatedAt)
    VALUES
        ( 1,  1, N'ACC-001', N'T-101', DATEADD(DAY, -150, @Now), N'Available', N'Good condition',                  DATEADD(DAY, -150, @Now)),
        ( 2,  1, N'ACC-002', N'T-101', DATEADD(DAY, -150, @Now), N'Borrowed',  N'Hardcover edition',               DATEADD(DAY, -150, @Now)),
        ( 3,  1, N'ACC-003', N'T-101', DATEADD(DAY, -120, @Now), N'Available', N'Paperback copy',                  DATEADD(DAY, -120, @Now)),
        ( 4,  2, N'ACC-004', N'T-102', DATEADD(DAY, -150, @Now), N'Available', N'Good condition',                  DATEADD(DAY, -150, @Now)),
        ( 5,  2, N'ACC-005', N'T-102', DATEADD(DAY, -150, @Now), N'Borrowed',  N'Good condition',                  DATEADD(DAY, -150, @Now)),
        ( 6,  3, N'ACC-006', N'T-103', DATEADD(DAY, -145, @Now), N'Available', N'20th Anniversary hardcover',      DATEADD(DAY, -145, @Now)),
        ( 7,  3, N'ACC-007', N'T-103', DATEADD(DAY, -145, @Now), N'Available', N'20th Anniversary paperback',      DATEADD(DAY, -145, @Now)),
        ( 8,  4, N'ACC-008', N'T-104', DATEADD(DAY, -140, @Now), N'Available', N'2nd Edition',                     DATEADD(DAY, -140, @Now)),
        ( 9,  4, N'ACC-009', N'T-104', DATEADD(DAY, -140, @Now), N'Borrowed',  N'2nd Edition',                     DATEADD(DAY, -140, @Now)),
        (10,  5, N'ACC-010', N'T-105', DATEADD(DAY, -140, @Now), N'Available', N'Reference shelf copy',            DATEADD(DAY, -140, @Now)),
        (11,  6, N'ACC-011', N'T-106', DATEADD(DAY, -140, @Now), N'Available', N'4th Edition',                     DATEADD(DAY, -140, @Now)),
        (12,  6, N'ACC-012', N'T-106', DATEADD(DAY, -100, @Now), N'Available', N'4th Edition',                     DATEADD(DAY, -100, @Now)),
        (13,  7, N'ACC-013', N'H-201', DATEADD(DAY, -135, @Now), N'Available', N'Paperback',                       DATEADD(DAY, -135, @Now)),
        (14,  7, N'ACC-014', N'H-201', DATEADD(DAY, -135, @Now), N'Borrowed',  N'Hardcover',                       DATEADD(DAY, -135, @Now)),
        (15,  7, N'ACC-015', N'H-201', DATEADD(DAY, -110, @Now), N'Available', N'Paperback',                       DATEADD(DAY, -110, @Now)),
        (16,  8, N'ACC-016', N'H-202', DATEADD(DAY, -135, @Now), N'Available', N'Good condition',                  DATEADD(DAY, -135, @Now)),
        (17,  9, N'ACC-017', N'E-301', DATEADD(DAY, -130, @Now), N'Available', N'Good condition',                  DATEADD(DAY, -130, @Now)),
        (18, 10, N'ACC-018', N'S-101', DATEADD(DAY, -130, @Now), N'Borrowed',  N'Illustrated edition',             DATEADD(DAY, -130, @Now)),
        (19, 10, N'ACC-019', N'S-101', DATEADD(DAY, -130, @Now), N'Available', N'Paperback',                       DATEADD(DAY, -130, @Now)),
        (20, 11, N'ACC-020', N'S-102', DATEADD(DAY, -130, @Now), N'Available', N'Good condition',                  DATEADD(DAY, -130, @Now)),
        (21, 12, N'ACC-021', N'S-103', DATEADD(DAY, -125, @Now), N'Available', N'Good condition',                  DATEADD(DAY, -125, @Now)),
        (22, 13, N'ACC-022', N'B-101', DATEADD(DAY, -125, @Now), N'Borrowed',  N'Hardcover',                       DATEADD(DAY, -125, @Now)),
        (23, 13, N'ACC-023', N'B-101', DATEADD(DAY, -125, @Now), N'Available', N'Paperback',                       DATEADD(DAY, -125, @Now)),
        (24, 14, N'ACC-024', N'B-102', DATEADD(DAY, -120, @Now), N'Available', N'Good condition',                  DATEADD(DAY, -120, @Now)),
        (25, 15, N'ACC-025', N'B-103', DATEADD(DAY, -120, @Now), N'Available', N'Good condition',                  DATEADD(DAY, -120, @Now)),
        (26, 16, N'ACC-026', N'H-203', DATEADD(DAY, -120, @Now), N'Available', N'Good condition',                  DATEADD(DAY, -120, @Now)),
        (27, 17, N'ACC-027', N'M-101', DATEADD(DAY, -115, @Now), N'Borrowed',  N'Hardcover collector copy',        DATEADD(DAY, -115, @Now)),
        (28, 17, N'ACC-028', N'M-101', DATEADD(DAY, -115, @Now), N'Available', N'Paperback',                       DATEADD(DAY, -115, @Now)),
        (29, 18, N'ACC-029', N'M-102', DATEADD(DAY, -115, @Now), N'Available', N'Paperback',                       DATEADD(DAY, -115, @Now)),
        (30, 18, N'ACC-030', N'M-102', DATEADD(DAY, -115, @Now), N'Available', N'Paperback',                       DATEADD(DAY, -115, @Now)),
        (31, 19, N'ACC-031', N'M-103', DATEADD(DAY, -110, @Now), N'Borrowed',  N'Good condition',                  DATEADD(DAY, -110, @Now)),
        (32, 20, N'ACC-032', N'FN-101',DATEADD(DAY, -110, @Now), N'Available', N'Hardcover',                       DATEADD(DAY, -110, @Now)),
        (33, 20, N'ACC-033', N'FN-101',DATEADD(DAY, -110, @Now), N'Available', N'Paperback',                       DATEADD(DAY, -110, @Now)),
        (34, 21, N'ACC-034', N'FN-102',DATEADD(DAY, -105, @Now), N'Available', N'Paperback',                       DATEADD(DAY, -105, @Now)),
        (35, 22, N'ACC-035', N'F-101', DATEADD(DAY, -105, @Now), N'Borrowed',  N'Centennial edition',              DATEADD(DAY, -105, @Now)),
        (36, 22, N'ACC-036', N'F-101', DATEADD(DAY, -105, @Now), N'Available', N'Paperback',                       DATEADD(DAY, -105, @Now)),
        (37, 23, N'ACC-037', N'F-102', DATEADD(DAY, -100, @Now), N'Available', N'Anniversary edition',             DATEADD(DAY, -100, @Now)),
        (38, 23, N'ACC-038', N'F-102', DATEADD(DAY, -100, @Now), N'Available', N'Paperback',                       DATEADD(DAY, -100, @Now)),
        (39, 24, N'ACC-039', N'E-302', DATEADD(DAY,  -95, @Now), N'Available', N'Good condition',                  DATEADD(DAY,  -95, @Now)),
        (40, 24, N'ACC-040', N'E-302', DATEADD(DAY,  -95, @Now), N'Available', N'Good condition',                  DATEADD(DAY,  -95, @Now)),
        (41, 25, N'ACC-041', N'E-303', DATEADD(DAY,  -90, @Now), N'Available', N'Good condition',                  DATEADD(DAY,  -90, @Now)),
        (42, 25, N'ACC-042', N'E-303', DATEADD(DAY,  -90, @Now), N'Available', N'Good condition',                  DATEADD(DAY,  -90, @Now)),
        (43,  6, N'ACC-043', N'T-106', DATEADD(DAY,  -85, @Now), N'Available', N'Spare reading room copy',         DATEADD(DAY,  -85, @Now)),
        (44, 13, N'ACC-044', N'B-101', DATEADD(DAY,  -80, @Now), N'Damaged',   N'Binding torn, awaiting repair',   DATEADD(DAY,  -80, @Now)),
        (45, 21, N'ACC-045', N'FN-102',DATEADD(DAY,  -75, @Now), N'Lost',      N'Reported missing during audit',   DATEADD(DAY,  -75, @Now));
    SET IDENTITY_INSERT dbo.BookCopies OFF;

    -- Loans (15 total: 6 Returned across past months, 5 Active Issued incl. 1 due today, 4 Overdue)
    SET IDENTITY_INSERT dbo.Loans ON;
    INSERT INTO dbo.Loans
        (LoanId, MemberId, CopyId, IssueDate, DueDate, ReturnDate, LoanStatus, FineAmount, RenewalCount, Notes, CreatedAt)
    VALUES
        -- Returned loans (earlier months + recent)
        ( 1,  1,  1, DATEADD(DAY, -110, @Now), DATEADD(DAY,  -96, @Now), DATEADD(DAY,  -98, @Now), N'Returned',  0.00, 0, N'Returned on time',                 DATEADD(DAY, -110, @Now)),
        ( 2,  2,  6, DATEADD(DAY,  -90, @Now), DATEADD(DAY,  -76, @Now), DATEADD(DAY,  -72, @Now), N'Returned', 20.00, 0, N'Returned 4 days late (Rs 5/day)',  DATEADD(DAY,  -90, @Now)),
        ( 3,  3, 13, DATEADD(DAY,  -75, @Now), DATEADD(DAY,  -61, @Now), DATEADD(DAY,  -61, @Now), N'Returned',  0.00, 1, N'Renewed once, returned on due date',DATEADD(DAY, -75, @Now)),
        ( 4,  4, 19, DATEADD(DAY,  -55, @Now), DATEADD(DAY,  -41, @Now), DATEADD(DAY,  -35, @Now), N'Returned', 30.00, 0, N'Returned 6 days late (Rs 5/day)',  DATEADD(DAY,  -55, @Now)),
        ( 5,  5, 28, DATEADD(DAY,  -40, @Now), DATEADD(DAY,  -26, @Now), DATEADD(DAY,  -28, @Now), N'Returned',  0.00, 0, N'Returned early',                   DATEADD(DAY,  -40, @Now)),
        ( 6,  6, 36, DATEADD(DAY,  -25, @Now), DATEADD(DAY,  -11, @Now), DATEADD(DAY,  -12, @Now), N'Returned',  0.00, 0, N'Returned in good shape',           DATEADD(DAY,  -25, @Now)),

        -- Active issued loans (unreturned, copy is Borrowed)
        ( 7,  1,  2, DATEADD(DAY,  -14, @Now), @Now,                     NULL,                     N'Issued',    0.00, 0, N'Due today',                        DATEADD(DAY,  -14, @Now)),
        ( 8,  7,  5, DATEADD(DAY,  -11, @Now), DATEADD(DAY,    3, @Now), NULL,                     N'Issued',    0.00, 0, N'Due in 3 days',                    DATEADD(DAY,  -11, @Now)),
        ( 9,  8, 14, DATEADD(DAY,   -8, @Now), DATEADD(DAY,    6, @Now), NULL,                     N'Issued',    0.00, 0, N'Active loan',                      DATEADD(DAY,   -8, @Now)),
        (10,  9, 22, DATEADD(DAY,  -16, @Now), DATEADD(DAY,    9, @Now), NULL,                     N'Issued',    0.00, 1, N'Extended by 7 days',               DATEADD(DAY,  -16, @Now)),
        (11, 10, 35, DATEADD(DAY,   -4, @Now), DATEADD(DAY,   10, @Now), NULL,                     N'Issued',    0.00, 0, N'Recent checkout',                  DATEADD(DAY,   -4, @Now)),

        -- Overdue unreturned loans (DueDate in past, ReturnDate IS NULL, copy is Borrowed)
        (12, 11,  9, DATEADD(DAY,  -24, @Now), DATEADD(DAY,  -10, @Now), NULL,                     N'Overdue',   0.00, 0, N'10 days overdue',                  DATEADD(DAY,  -24, @Now)),
        (13, 12, 18, DATEADD(DAY,  -21, @Now), DATEADD(DAY,   -7, @Now), NULL,                     N'Overdue',   0.00, 0, N'7 days overdue',                   DATEADD(DAY,  -21, @Now)),
        (14, 13, 27, DATEADD(DAY,  -26, @Now), DATEADD(DAY,   -5, @Now), NULL,                     N'Overdue',   0.00, 1, N'5 days overdue after 1 renewal',   DATEADD(DAY,  -26, @Now)),
        (15, 14, 31, DATEADD(DAY,  -17, @Now), DATEADD(DAY,   -3, @Now), NULL,                     N'Overdue',   0.00, 0, N'3 days overdue',                   DATEADD(DAY,  -17, @Now));
    SET IDENTITY_INSERT dbo.Loans OFF;

    -- LoanRenewals (matching loans 3, 10, 14 which have RenewalCount = 1)
    INSERT INTO dbo.LoanRenewals (LoanId, PreviousDueDate, NewDueDate, RenewalDate)
    VALUES
        ( 3, DATEADD(DAY, -68, @Now), DATEADD(DAY, -61, @Now), DATEADD(DAY, -69, @Now)),
        (10, DATEADD(DAY,   2, @Now), DATEADD(DAY,   9, @Now), DATEADD(DAY,  -2, @Now)),
        (14, DATEADD(DAY, -12, @Now), DATEADD(DAY,  -5, @Now), DATEADD(DAY, -13, @Now));

    -- Reservations (optional queue module)
    INSERT INTO dbo.Reservations (MemberId, BookId, ReservationDate, ReservationStatus, FulfilledDate)
    VALUES
        (15,  4, DATEADD(DAY, -3, @Now), N'Pending',   NULL),
        (16, 17, DATEADD(DAY, -2, @Now), N'Pending',   NULL),
        ( 4,  1, DATEADD(DAY, -20,@Now), N'Fulfilled', DATEADD(DAY, -15, @Now));
END

COMMIT TRANSACTION;
GO
