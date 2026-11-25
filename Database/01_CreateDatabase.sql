-- Create LibraryManagementDB if it does not exist yet.

IF DB_ID(N'LibraryManagementDB') IS NULL
BEGIN
    CREATE DATABASE [LibraryManagementDB];
END
GO

USE [LibraryManagementDB];
GO
