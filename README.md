ZOOM API DETAILS

1. Create an App in Zoom Marketplace with the following details:
- App Name: MTL-VCBooking
- App Type: OAuth
- Redirect URL: http://localhost:8080/VCBooking/ZoomAuth.aspx
- OAuth Scopes: 
  - view:meeting_all
  - create:meeting
  - update:meeting
  - delete:meeting

2. Add Zoom Client ID, Client Secret, and Zoom Account ID to the VC_Account_Master table in the TMG_Employeedata database.  

USE TMG_Employeedata;
GO

-- ====================== ADDING ZOOM COLUMNS (safe) ======================
IF NOT EXISTS (SELECT 1 FROM sys.columns 
               WHERE object_id = OBJECT_ID('dbo.VC_Account_Master') 
                 AND name = 'ZoomClientId')
BEGIN
    ALTER TABLE dbo.VC_Account_Master
    ADD ZoomClientId     NVARCHAR(255) NULL,
        ZoomClientSecret NVARCHAR(255) NULL,
        ZoomAccountId    NVARCHAR(255) NULL;
END
GO

UPDATE VC_Account_Master
SET ZoomClientId = '',
    ZoomClientSecret = '',
    ZoomAccountId = ''
WHERE VCTypeId = 1;
GO

-- Safe column drop (one by one)
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.VC_Account_Master') AND name = 'APIKey')
    ALTER TABLE dbo.VC_Account_Master DROP COLUMN APIKey;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.VC_Account_Master') AND name = 'Parameter1')
    ALTER TABLE dbo.VC_Account_Master DROP COLUMN Parameter1;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.VC_Account_Master') AND name = 'Parameter2')
    ALTER TABLE dbo.VC_Account_Master DROP COLUMN Parameter2;
GO

UPDATE VC_Account_Master
SET APIURL = 'https://api.zoom.us'
WHERE VCAccountId = 3;
GO

-- ====================== ADDING EMAIL COLUMNS (safe) ======================
IF NOT EXISTS (SELECT 1 FROM sys.columns 
               WHERE object_id = OBJECT_ID('dbo.VC_Account_Master') 
                 AND name = 'VC_Email')
BEGIN
    ALTER TABLE dbo.VC_Account_Master
    ADD VC_Email          NVARCHAR(255) NULL,
        VC_Email_Password NVARCHAR(255) NULL;
END
GO

UPDATE VC_Account_Master
SET VC_Email = 'mtlvcsystem@gmail.com',
    VC_Email_Password = 'atkazpgdrxurymrz'
WHERE VCTypeId IN (1,2);
GO

-- ====================== RECREATE VCRequestLog TABLE ======================
-- (No need for separate ALTER ADD anymore - we recreate the whole table)
IF OBJECT_ID('dbo.VCRequestLog', 'U') IS NOT NULL
    DROP TABLE dbo.VCRequestLog;
GO

CREATE TABLE VCRequestLog
(
    LogId BIGINT IDENTITY(1,1) PRIMARY KEY,
    LogDate DATETIME,
    LogType VARCHAR(20),
    VCId VARCHAR(20),
    CreatedBy VARCHAR(50),
    CreatedDate DATETIME,
    CompanyId INT,
    Topic NVARCHAR(100),
    VCDate DATETIME,
    VCTypeId INT,
    VCAccountId INT,
    FromTime DATETIME,
    ToTime DATETIME,
    ParticipantCount INT,
    LocationId INT,
    UnitFloorDetails VARCHAR(100),
    VCDetails VARCHAR(1000),
    VCStatus VARCHAR(20),
    VCBookingDetails NVARCHAR(MAX),
    AutomationFlag VARCHAR(20),
    BookedBy VARCHAR(50),
    BookingDate DATETIME,
    CancelledBy VARCHAR(50),
    CancelledDate DATETIME,
    MeetingId NVARCHAR(50),
    JoinUrl NVARCHAR(500),
    HostUrl NVARCHAR(500),
    MeetingPassword NVARCHAR(50),
    Platform NVARCHAR(50),
    APIStatus NVARCHAR(50),
    CancelReason NVARCHAR(MAX)
);
GO

-- ====================== UPDATE TRIGGER ======================
CREATE OR ALTER TRIGGER trg_Update_VCRequestLog
ON VCRequestHeader
AFTER UPDATE
AS
BEGIN
    INSERT INTO VCRequestLog (
        LogDate, LogType, VCId, CreatedBy, CreatedDate, CompanyId, Topic,
        VCDate, VCTypeId, VCAccountId, FromTime, ToTime, ParticipantCount,
        LocationId, UnitFloorDetails, VCDetails, VCStatus, VCBookingDetails,
        AutomationFlag, BookedBy, BookingDate, CancelledBy, CancelledDate,
        MeetingId, JoinUrl, HostUrl, MeetingPassword, Platform, APIStatus, CancelReason
    )
    SELECT 
        GETDATE(),
        CASE 
            WHEN i.VCStatus = 'Booked' AND d.VCStatus IS NULL THEN 'Booked'
            WHEN i.VCStatus = 'Cancelled' THEN 'Cancelled'
            WHEN i.FromTime <> d.FromTime OR i.ToTime <> d.ToTime THEN 'Rescheduled'
            ELSE 'New'
        END,
        i.VCId, i.CreatedBy, i.CreatedDate, i.CompanyId, i.Topic,
        i.VCDate, i.VCTypeId, i.VCAccountId, i.FromTime, i.ToTime,
        i.ParticipantCount, i.LocationId, i.UnitFloorDetails, i.VCDetails,
        i.VCStatus, i.VCBookingDetails, i.AutomationFlag, i.BookedBy,
        i.BookingDate, i.CancelledBy, i.CancelledDate,
        i.MeetingId, i.JoinUrl, i.HostUrl, i.MeetingPassword,
        i.Platform, i.APIStatus, i.CancelReason
    FROM inserted i
    LEFT JOIN deleted d ON i.VCId = d.VCId
    WHERE i.VCStatus <> 'Deleted';
END;
GO

-- ====================== DELETE TRIGGER ======================
CREATE OR ALTER TRIGGER trg_Delete_VCRequestLog
ON VCRequestHeader
AFTER DELETE
AS
BEGIN
    INSERT INTO VCRequestLog
    (
        LogDate, LogType, VCId, CreatedBy, CreatedDate, CompanyId, Topic, VCDate,
        VCTypeId, VCAccountId, FromTime, ToTime, ParticipantCount,
        LocationId, UnitFloorDetails, VCDetails, VCStatus,
        VCBookingDetails, AutomationFlag, BookedBy, BookingDate,
        CancelledBy, CancelledDate, MeetingId, JoinUrl, HostUrl, 
        MeetingPassword, Platform, APIStatus, CancelReason
    )
    SELECT 
        GETDATE(), 'DELETE',
        VCId, CreatedBy, CreatedDate, CompanyId, Topic, VCDate,
        VCTypeId, VCAccountId, FromTime, ToTime, ParticipantCount,
        LocationId, UnitFloorDetails, VCDetails, VCStatus,
        VCBookingDetails, AutomationFlag, BookedBy, BookingDate,
        CancelledBy, CancelledDate, MeetingId, JoinUrl, HostUrl, 
        MeetingPassword, Platform, 'Deleted', CancelReason
    FROM deleted;
END;
GO

-- ====================== LOCATION MASTER (safe insert) ======================
MERGE dbo.Location_Master AS target
USING (VALUES 
    (4,'Board Room 2','BR2','Active','Admin'),
    (5,'Board Room 2','BR2','Active','Admin'),
    (6,'Ground Floor','G','Active','Admin'),
    (7,'Conference Room 3','CR3','Active','Admin'),
    (8,'Board Room 3','BR3','Active','Admin')
) AS source (LocationId, LocationName, LocationShortName, Status, CreatedBy)
ON target.LocationId = source.LocationId
WHEN NOT MATCHED BY TARGET THEN
    INSERT (LocationId, LocationName, LocationShortName, Status, CreatedBy)
    VALUES (source.LocationId, source.LocationName, source.LocationShortName, source.Status, source.CreatedBy);
GO

-- ====================== VC PARTICIPANTS LOG TABLE ======================
IF OBJECT_ID('dbo.VCParticipantsLog', 'U') IS NOT NULL
    DROP TABLE dbo.VCParticipantsLog;
GO

CREATE TABLE VCParticipantsLog
(
    LogId BIGINT IDENTITY(1,1) PRIMARY KEY,
    LogDate DATETIME DEFAULT GETDATE(),
    LogType NVARCHAR(50),
    ParticipantId INT,
    VCId NVARCHAR(50),
    ParticipantEmail NVARCHAR(255),
    LocationId INT,
    LocationName NVARCHAR(255),
    CreatedBy NVARCHAR(100),
    CreatedDate DATETIME,
    UpdatedBy NVARCHAR(100),
    UpdatedDate DATETIME
);
GO

-- ====================== PARTICIPANT INSERT TRIGGER ======================
CREATE OR ALTER TRIGGER trg_Insert_VCParticipantsLog
ON VCParticipants
AFTER INSERT
AS
BEGIN
    INSERT INTO VCParticipantsLog
    (
        LogDate, LogType, ParticipantId, VCId, ParticipantEmail,
        LocationId, LocationName, CreatedBy, CreatedDate
    )
    SELECT 
        GETDATE(), 'Added',
        i.ParticipantId, i.VCId, i.ParticipantEmail,
        i.LocationId, i.LocationName,
        i.CreatedBy, i.CreatedDate
    FROM inserted i;
END;
GO

-- ====================== PARTICIPANT DELETE TRIGGER ======================
CREATE OR ALTER TRIGGER trg_Delete_VCParticipantsLog
ON VCParticipants
AFTER DELETE
AS
BEGIN
    INSERT INTO VCParticipantsLog
    (
        LogDate, LogType, ParticipantId, VCId, ParticipantEmail,
        LocationId, LocationName, CreatedBy, CreatedDate
    )
    SELECT 
        GETDATE(), 'Deleted',
        d.ParticipantId, d.VCId, d.ParticipantEmail,
        d.LocationId, d.LocationName,
        d.CreatedBy, d.CreatedDate
    FROM deleted d;
END;
GO

-- Final check
SELECT * FROM dbo.VC_Account_Master;
GO

-------------------------------------------------------------------------------------------------------
-- GOOGLE MEET API CONFIGURATION                                            

use TMG_Employeedata;

-- 1. First, add the MeetingRoomUrl column if it doesn't exist yet
ALTER TABLE VC_Account_Master
ADD MeetingRoomUrl NVARCHAR(500) NULL;
GO

-- 2. Then update the accounts with their static Google Meet URLs
UPDATE VC_Account_Master 
SET MeetingRoomUrl = 'https://meet.google.com/hra-jccr-xgx' 
WHERE VCAccountId = 4;

UPDATE VC_Account_Master 
SET MeetingRoomUrl = 'https://meet.google.com/enp-bnve-ukt' 
WHERE VCAccountId = 5;

UPDATE VC_Account_Master 
SET MeetingRoomUrl = 'https://meet.google.com/brz-nbgp-asg' 
WHERE VCAccountId = 6;
GO
