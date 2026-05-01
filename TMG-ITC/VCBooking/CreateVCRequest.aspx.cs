using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;

namespace VCBooking
{
    public partial class CreateVCRequest : System.Web.UI.Page
    {
        protected async void Page_Load(object sender, EventArgs e)
        {
            Page.MaintainScrollPositionOnPostBack = true;

            if (Session["EmployeeCode"] == null)
            {
                Response.Redirect("~/Login.aspx",false);
            }
            if (!IsPostBack)
            {
                // 1. Basic Dropdowns
                await CleanupExpiredMeetings();
                DataTable dt = new DataTable();
                dt.Columns.Add("ParticipantEmail");
                ViewState["Participants"] = dt;

                LoadDropdown("SELECT CompanyName,CompanyId from dbo.Company_Master WHERE Status='Active'",
                    ddlCompany, "CompanyName", "CompanyId", "-- Select Your Company --");

                LoadDropdown("SELECT VCTypeName,VCTypeId from dbo.VC_Type_Master WHERE Status='Active'",
                    ddlVCType, "VCTypeName", "VCTypeId", "-- Select Vedio Conference Type --");

                LoadDropdown("SELECT LocationName,LocationId from dbo.Location_Master WHERE Status='Active'",
                    ddlLocation, "LocationName", "LocationId", "-- Select Location --");

                // Initialize Account dropdown
                ddlVCAccount.Items.Clear();
                ddlVCAccount.Items.Add(new ListItem("-- Select Account --", ""));

                // 2. Populate Hour Dropdown
                ddlFromHour.Items.Clear();
                ddlFromHour.Items.Add(new ListItem("Hr", ""));
                for (int i = 0; i < 24; i++)
                {
                    ddlFromHour.Items.Add(new ListItem(i.ToString("00"), i.ToString()));
                }

                // 3. Automation Logic (Next 15-Minute Slot)
                DateTime now = DateTime.Now;
                int minutesSinceMidnight = now.Hour * 60 + now.Minute;
                
                // Add 1 min then divide by 15 + 1 to get the next interval
                // e.g. 10:00 -> 600 -> next 615 -> 10:15
                // e.g. 10:14 -> 614 -> next 615 -> 10:15
                // e.g. 10:15 -> 615 -> next 630 -> 10:30
                int nextInterval = ((minutesSinceMidnight / 15) + 1) * 15;
                
                DateTime targetTime = now.Date.AddMinutes(nextInterval);
                
                txtDate.Text = targetTime.ToString("yyyy-MM-dd");
                ddlFromHour.SelectedValue = targetTime.Hour.ToString();
                ddlFromMinute.SelectedValue = targetTime.Minute.ToString();

                // 4. Default Duration (15 min)
                ddlHours.SelectedValue = "0";
                ddlMinutes.SelectedValue = "15";

                // 5. Trigger Initial Availability View
                // Note: ddlVCType is still --Select--, so it might not load accounts yet, 
                // but at least BookedSlots will show today's calendar.
                LoadBookedSlots();
            }

        }

        protected void btnAddParticipant_Click(object sender, EventArgs e)
        {
            lblParticipantMessage.Text = "";

            if (!string.IsNullOrWhiteSpace(txtParticipant.Text))
            {
                string[] emails = txtParticipant.Text.Split(',');

                DataTable dt = ViewState["Participants"] as DataTable;

                if (dt == null)
                {
                    dt = new DataTable();
                    dt.Columns.Add("ParticipantEmail");
                }

                foreach (string email in emails)
                {
                    string cleanEmail = email.Trim().ToLower();

                    if (string.IsNullOrEmpty(cleanEmail))
                        continue;

                    bool exists = false;

                    foreach (DataRow row in dt.Rows)
                    {
                        if (row["ParticipantEmail"].ToString().ToLower() == cleanEmail)
                        {
                            exists = true;
                            break;
                        }
                    }

                    if (!exists)
                    {
                        DataRow dr = dt.NewRow();
                        dr["ParticipantEmail"] = cleanEmail;
                        dt.Rows.Add(dr);
                    }
                }

                ViewState["Participants"] = dt;

                gvParticipants.DataSource = dt;
                gvParticipants.DataBind();

                txtParticipant.Text = "";
            }

        }


        private void LoadDropdown(string query, DropDownList ddl, string textField, string valueField, string placeholderText)
        {
            string connStr = ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connStr))
            {

                SqlCommand cmd = new SqlCommand(query, conn);

                conn.Open();

                SqlDataReader reader = cmd.ExecuteReader();

                ddl.DataSource = reader;
                ddl.DataTextField = textField;
                ddl.DataValueField = valueField;
                ddl.DataBind();
                ddl.Items.Insert(0, new ListItem(placeholderText, ""));
            }
        }

        protected void ddlVCType_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadAvailableAccounts();
        }


        protected async void btnFormSubmit_Click(object sender, EventArgs e)
        {
            try
            {
                if (ddlCompany.SelectedValue == "" ||
                    ddlVCType.SelectedValue == "" ||
                    ddlVCAccount.SelectedValue == "" ||
                    ddlLocation.SelectedValue == "" ||
                    string.IsNullOrEmpty(txtTopic.Text) ||
                    string.IsNullOrEmpty(txtDate.Text) ||
                    (ddlFromHour.SelectedValue == "" || ddlFromMinute.SelectedValue == "") ||
                    (ddlHours.SelectedValue == "" && ddlMinutes.SelectedValue == ""))
                {
                    ScriptManager.RegisterStartupScript(this, GetType(), "alert", "alert('Please fill all required fields');", true);
                    return;
                }

                DataTable dt = ViewState["Participants"] as DataTable;
                if (dt == null || dt.Rows.Count == 0)
                {
                    ScriptManager.RegisterStartupScript(this, GetType(), "alert", "alert('Add at least one participant');", true);
                    return;
                }

                string connStr = ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString;
                List<string> participantEmails = new List<string>();
                foreach (DataRow row in dt.Rows)
                    participantEmails.Add(row["ParticipantEmail"].ToString());

                string createdByName = Session["UserName"] != null ? Session["UserName"].ToString() : null;
                string createdByEmail = Session["UserEmail"] != null ? Session["UserEmail"].ToString() : null;

                if (string.IsNullOrEmpty(createdByName) || string.IsNullOrEmpty(createdByEmail))
                {
                    Response.Redirect("Login.aspx");
                    return;
                }

                string meetingId = "";
                string joinUrl = "";
                string startUrl = "";
                string password = "";
                DateTime fullFromDateTime;
                DateTime fullToDateTime;

                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    await conn.OpenAsync();
                    using (SqlTransaction transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            string newVCId = "";
                            string getLastIdQuery = "SELECT TOP 1 VCId FROM VCRequestHeader ORDER BY VCId DESC";
                            SqlCommand cmdGetId = new SqlCommand(getLastIdQuery, conn, transaction);
                            object result = cmdGetId.ExecuteScalar();

                            if (result == null)
                                newVCId = "VC001";
                            else
                            {
                                string lastId = result.ToString();
                                int number = int.Parse(lastId.Substring(2)) + 1;
                                newVCId = "VC" + number.ToString("D3");
                            }


                            int fromHour = int.Parse(ddlFromHour.SelectedValue);
                            int fromMinute = int.Parse(ddlFromMinute.SelectedValue);

                            fullFromDateTime = DateTime.Parse(txtDate.Text)
                                                    .AddHours(fromHour)
                                                    .AddMinutes(fromMinute);

                            int hours = string.IsNullOrEmpty(ddlHours.SelectedValue) ? 0 : int.Parse(ddlHours.SelectedValue);
                            int minutes = string.IsNullOrEmpty(ddlMinutes.SelectedValue) ? 0 : int.Parse(ddlMinutes.SelectedValue);

                            int duration = (hours * 60) + minutes;

                            // ❗ Prevent 0 duration
                            if (duration == 0)
                            {
                                ScriptManager.RegisterStartupScript(this, GetType(), "alert",
                                    "alert('Please select valid duration');", true);
                                return;
                            }

                            fullToDateTime = fullFromDateTime.AddMinutes(duration);

                            string overlapCheckQuery = @"
                                SELECT COUNT(*) FROM VCRequestHeader
                                WHERE VCAccountId = @VCAccountId
                                AND VCStatus NOT IN ('Cancelled', 'Completed')
                                AND (@NewFromTime < ToTime AND @NewToTime > FromTime)";

                            SqlCommand cmdCheck = new SqlCommand(overlapCheckQuery, conn, transaction);
                            cmdCheck.Parameters.AddWithValue("@VCAccountId", ddlVCAccount.SelectedValue);
                            cmdCheck.Parameters.Add("@NewFromTime", SqlDbType.DateTime).Value = fullFromDateTime;
                            cmdCheck.Parameters.Add("@NewToTime", SqlDbType.DateTime).Value = fullToDateTime;

                            if ((int)cmdCheck.ExecuteScalar() > 0)
                            {
                                transaction.Rollback();
                                ScriptManager.RegisterStartupScript(this, GetType(), "alert", "alert('Selected VC Account is already booked!');", true);
                                return;
                            }

                            int participantCount = participantEmails.Count;

                            string insertHeaderQuery = @"
                                INSERT INTO VCRequestHeader
                                (VCId, CompanyId, VCTypeId, VCAccountId, Topic, VCDate, FromTime, ToTime,ParticipantCount, LocationId,UnitFloorDetails, VCDetails,VCStatus, CreatedBy, CreatedDate)
                                VALUES
                                (@VCId, @CompanyId, @VCTypeId, @VCAccountId, @Topic, @VCDate, @FromTime, @ToTime, @ParticipantCount, @LocationId, @UnitFloorDetails, @VCDetails,'New', @CreatedBy, GETDATE())";

                            SqlCommand cmdHeader = new SqlCommand(insertHeaderQuery, conn, transaction);
                            cmdHeader.Parameters.AddWithValue("@VCId", newVCId);
                            cmdHeader.Parameters.AddWithValue("@CompanyId", ddlCompany.SelectedValue);
                            cmdHeader.Parameters.AddWithValue("@VCTypeId", ddlVCType.SelectedValue);
                            cmdHeader.Parameters.AddWithValue("@VCAccountId", ddlVCAccount.SelectedValue);
                            cmdHeader.Parameters.AddWithValue("@Topic", txtTopic.Text.Trim());
                            cmdHeader.Parameters.Add("@VCDate", SqlDbType.DateTime).Value = DateTime.Parse(txtDate.Text);
                            cmdHeader.Parameters.Add("@FromTime", SqlDbType.DateTime).Value = fullFromDateTime;
                            cmdHeader.Parameters.Add("@ToTime", SqlDbType.DateTime).Value = fullToDateTime;
                            cmdHeader.Parameters.AddWithValue("@ParticipantCount", participantCount);
                            cmdHeader.Parameters.AddWithValue("@LocationId", ddlLocation.SelectedValue);
                            cmdHeader.Parameters.AddWithValue("@UnitFloorDetails", txtUnitFloor.Text);
                            cmdHeader.Parameters.AddWithValue("@VCDetails", txtVCDetails.Text);
                            cmdHeader.Parameters.AddWithValue("@CreatedBy", createdByName);
                            cmdHeader.ExecuteNonQuery();

                            foreach (string email in participantEmails)
                            {
                                SqlCommand cmdParticipant = new SqlCommand(@"
                                  INSERT INTO VCParticipants 
                                  (VCId, ParticipantEmail, LocationId, LocationName, CreatedBy, CreatedDate)
                                  VALUES 
                                  (@VCId, @ParticipantEmail, @LocationId, @LocationName, @CreatedBy, GETDATE())", conn, transaction);

                                cmdParticipant.Parameters.AddWithValue("@VCId", newVCId);
                                cmdParticipant.Parameters.AddWithValue("@ParticipantEmail", email);

                                cmdParticipant.Parameters.AddWithValue("@LocationId", Convert.ToInt32(ddlLocation.SelectedValue));
                                cmdParticipant.Parameters.AddWithValue("@LocationName", ddlLocation.SelectedItem.Text);

                                cmdParticipant.Parameters.AddWithValue("@CreatedBy", createdByName);

                                cmdParticipant.ExecuteNonQuery();
                            }

                            string platform = ddlVCType.SelectedItem.Text;

                            if (platform.Contains("Zoom"))
                            {
                                var zoomService = new VCBooking.Services.ZoomService(ddlVCAccount.SelectedValue);
                                var zoomResponse = await zoomService.CreateMeetingAsync(
                                    txtTopic.Text.Trim(),
                                    fullFromDateTime,
                                    (int)(fullToDateTime - fullFromDateTime).TotalMinutes
                                );

                                meetingId = zoomResponse.id;
                                joinUrl = zoomResponse.join_url;
                                startUrl = zoomResponse.start_url;
                                password = zoomResponse.password;
                                platform = "Zoom";
                            }
                            else if (platform.Contains("Google") || platform.Contains("Teams")) // Handling "Teams" too if it hasn't been renamed yet
                            {
                                // Fetch the pre-configured room URL from the database
                                string roomUrl = "";
                                using (SqlConnection connRoom = new SqlConnection(ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString))
                                {
                                    SqlCommand cmdRoom = new SqlCommand("SELECT ISNULL(MeetingRoomUrl, '') FROM VC_Account_Master WHERE VCAccountId = @Id", connRoom);
                                    cmdRoom.Parameters.AddWithValue("@Id", ddlVCAccount.SelectedValue);
                                    connRoom.Open();
                                    object roomResult = cmdRoom.ExecuteScalar();
                                    if (roomResult != null) roomUrl = roomResult.ToString();
                                }

                                var googleService = new VCBooking.Services.GoogleMeetService(ddlVCAccount.SelectedValue);
                                var googleResponse = await googleService.CreateMeetingAsync(
                                    txtTopic.Text.Trim(),
                                    fullFromDateTime,
                                    (int)(fullToDateTime - fullFromDateTime).TotalMinutes,
                                    roomUrl,
                                    createdByName
                                );

                                meetingId = googleResponse.id;
                                joinUrl = googleResponse.join_url;
                                startUrl = googleResponse.join_url;
                                password = "";
                                platform = "Google Meet";
                            }

                            SqlCommand cmdUpdateMeeting = new SqlCommand(@"
                                UPDATE VCRequestHeader
                                SET MeetingId=@MeetingId, JoinUrl=@JoinUrl, HostUrl=@HostUrl,
                                    MeetingPassword=@MeetingPassword, Platform=@Platform, APIStatus='Success', VCStatus='Booked'
                                WHERE VCId=@VCId", conn, transaction);
                            cmdUpdateMeeting.Parameters.AddWithValue("@MeetingId", meetingId);
                            cmdUpdateMeeting.Parameters.AddWithValue("@JoinUrl", joinUrl);
                            cmdUpdateMeeting.Parameters.AddWithValue("@HostUrl", startUrl);
                            cmdUpdateMeeting.Parameters.AddWithValue("@MeetingPassword", password);
                            cmdUpdateMeeting.Parameters.AddWithValue("@Platform", platform);
                            cmdUpdateMeeting.Parameters.AddWithValue("@VCId", newVCId);
                            cmdUpdateMeeting.ExecuteNonQuery();

                            transaction.Commit();

                            // Send Emails Outside Transaction
                            try
                            {
                                var emailService = new VCBooking.Services.EmailService(ddlVCAccount.SelectedValue);
                                await emailService.SendMeetingInviteAsync(txtTopic.Text.Trim(), fullFromDateTime, 
                                    (int)(fullToDateTime - fullFromDateTime).TotalMinutes, joinUrl, password, participantEmails, platform);
                            }
                            catch (Exception exEmail) { System.Diagnostics.Debug.WriteLine("Email Error: " + exEmail.Message); }

                            hdnShowSuccess.Value = "1";
                        }
                        catch (Exception ex)
                        {
                            if (transaction != null && transaction.Connection != null) transaction.Rollback();
                            throw ex;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                string msg = ex.Message.Replace("'", "").Replace("\n", " ").Replace("\r", " ");
                ScriptManager.RegisterStartupScript(this, GetType(), "errorAlert", "alert('Error creating meeting: " + msg + "');", true);
            }
        }





        private void LoadAvailableAccounts()
        {
            try
            {
                if (ddlVCType.SelectedValue == "" ||
                    string.IsNullOrEmpty(txtDate.Text) ||
                    (ddlFromHour.SelectedValue == "" || ddlFromMinute.SelectedValue == "") ||
                    (ddlHours.SelectedValue == "" && ddlMinutes.SelectedValue == ""))
                {
                    ddlVCAccount.Items.Clear();
                    ddlVCAccount.Items.Insert(0, new ListItem("-- Select Account --", ""));
                    return;
                }

                string connStr = ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString;

                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    // 🔹 Query excludes already booked and active meetings
                    // We ignore 'Cancelled' and 'Completed' statuses
                    string query = @"
            SELECT a.VCAccountId, a.VCAccountName
            FROM VC_Account_Master a
            WHERE a.VCTypeId = @VCTypeId
            AND a.Status = 'Active'
            AND NOT EXISTS
            (
                SELECT 1
                FROM VCRequestHeader h
                WHERE h.VCAccountId = a.VCAccountId
                AND h.VCStatus NOT IN ('Cancelled', 'Completed')
                AND (@NewFromTime < h.ToTime AND @NewToTime > h.FromTime)
            )";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@VCTypeId", ddlVCType.SelectedValue);

                    // 🔹 Robust Parsing
                    DateTime newFrom, newTo;

                    // ✅ Get duration from Hours + Minutes dropdown
                    int hours = string.IsNullOrEmpty(ddlHours.SelectedValue) ? 0 : int.Parse(ddlHours.SelectedValue);
                    int minutes = string.IsNullOrEmpty(ddlMinutes.SelectedValue) ? 0 : int.Parse(ddlMinutes.SelectedValue);

                    int duration = (hours * 60) + minutes;

                    // ✅ Validate and calculate time
                    int fromHour = int.Parse(ddlFromHour.SelectedValue);
                    int fromMinute = int.Parse(ddlFromMinute.SelectedValue);

                    newFrom = DateTime.Parse(txtDate.Text)
                                .AddHours(fromHour)
                                .AddMinutes(fromMinute);

                    if (duration == 0)
                    {
                        return;
                    }

                    newTo = newFrom.AddMinutes(duration);

                    cmd.Parameters.Add("@NewFromTime", SqlDbType.DateTime).Value = newFrom;
                    cmd.Parameters.Add("@NewToTime", SqlDbType.DateTime).Value = newTo;

                    conn.Open();
                    SqlDataReader reader = cmd.ExecuteReader();

                    ddlVCAccount.DataSource = reader;
                    ddlVCAccount.DataTextField = "VCAccountName";
                    ddlVCAccount.DataValueField = "VCAccountId";
                    ddlVCAccount.DataBind();

                    ddlVCAccount.Items.Insert(0, new ListItem("-- Select Account --", ""));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error loading accounts: " + ex.Message);
                ddlVCAccount.Items.Clear();
                ddlVCAccount.Items.Insert(0, new ListItem("-- Error Loading Accounts --", ""));
            }
        }


        protected void DateOrTimeChanged(object sender, EventArgs e)
        {
            LoadAvailableAccounts();
            LoadBookedSlots();
        }

        private void LoadBookedSlots()
        {
            gvBookedSlots.DataSource = null;
            gvBookedSlots.DataBind();

            if (string.IsNullOrEmpty(txtDate.Text))
                return;

            string connStr = ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                string query = @"
                    SELECT
                        a.VCAccountName,
                        h.Topic,
                        h.FromTime,
                        h.ToTime,
                        h.VCStatus
                    FROM VCRequestHeader h
                    INNER JOIN VC_Account_Master a ON a.VCAccountId = h.VCAccountId
                    WHERE h.VCStatus IN ('Booked', 'Rescheduled')
                    AND CAST(h.VCDate AS DATE) = CAST(@SelectedDate AS DATE)
                    ORDER BY h.FromTime";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.Add("@SelectedDate", SqlDbType.Date).Value = DateTime.Parse(txtDate.Text);

                conn.Open();
                SqlDataAdapter da = new SqlDataAdapter(cmd);
                DataTable dt = new DataTable();
                da.Fill(dt);

                gvBookedSlots.DataSource = dt;
                gvBookedSlots.DataBind();
            }
        }


        private async Task CleanupExpiredMeetings()
        {
            string connStr = ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();

                // ✅ STEP 1: Include VCAccountId and Platform
                string query = @"
            SELECT VCId, MeetingId, VCAccountId, Platform
            FROM VCRequestHeader
            WHERE DATEADD(MINUTE, 10, ToTime) < GETDATE()
            AND VCStatus IN ('Booked', 'Rescheduled')
            AND MeetingId IS NOT NULL";

                SqlCommand cmd = new SqlCommand(query, conn);
                SqlDataReader reader = await cmd.ExecuteReaderAsync();

                // ✅ STEP 2: Store VCAccountId and Platform
                var expiredMeetings = new List<Tuple<string, string, string, string>>();

                while (await reader.ReadAsync())
                {
                    expiredMeetings.Add(new Tuple<string, string, string, string>(
                        reader["VCId"].ToString(),          // VCId
                        reader["MeetingId"].ToString(),     // MeetingId
                        reader["VCAccountId"].ToString(),    // VCAccountId
                        reader["Platform"].ToString()       // Platform
                    ));
                }

                reader.Close();

                // ❌ REMOVE this line (very important)
                // var zoomService = new VCBooking.Services.ZoomService();

                // ✅ STEP 3: Create ZoomService inside loop
                // ✅ STEP 3: Create Service inside loop
                foreach (var meeting in expiredMeetings)
                {
                    try
                    {
                        string vcId = meeting.Item1;
                        string meetingId = meeting.Item2;
                        string vcAccountId = meeting.Item3;
                        string platform = meeting.Item4;

                        if (platform == "Zoom")
                        {
                            var zoomService = new VCBooking.Services.ZoomService(vcAccountId);
                            await zoomService.DeleteMeetingAsync(meetingId);
                        }
                        else if (platform == "Google Meet")
                        {
                            var googleService = new VCBooking.Services.GoogleMeetService(vcAccountId);
                            await googleService.DeleteMeetingAsync(meetingId);
                        }

                        string updateQuery = @"
                    UPDATE VCRequestHeader
                    SET VCStatus = 'Completed',
                        APIStatus = 'Deleted'
                    WHERE VCId = @VCId";

                        SqlCommand updateCmd = new SqlCommand(updateQuery, conn);
                        updateCmd.Parameters.AddWithValue("@VCId", vcId);

                        await updateCmd.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("Error Occured in createRequest page: " + ex.Message);
                    }
                }
            }
        }
    }
}