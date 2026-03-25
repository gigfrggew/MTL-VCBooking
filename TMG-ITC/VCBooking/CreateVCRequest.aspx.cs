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
                Response.Redirect("~/Login.aspx");
            }
            if (!IsPostBack)
            {
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
            }
            if (!IsPostBack)
            {
                ddlFromHour.Items.Clear();
                ddlFromHour.Items.Add(new ListItem("Hr", ""));

                for (int i = 0; i < 24; i++)
                {
                    ddlFromHour.Items.Add(new ListItem(i.ToString("00"), i.ToString()));
                }
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
                                AND CAST(VCDate AS DATE) = CAST(@NewFromTime AS DATE)
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

                            string insertHeaderQuery = @"
                                INSERT INTO VCRequestHeader
                                (VCId, CompanyId, VCTypeId, VCAccountId, Topic, VCDate, FromTime, ToTime, LocationId, VCStatus, CreatedBy, CreatedDate)
                                VALUES
                                (@VCId, @CompanyId, @VCTypeId, @VCAccountId, @Topic, @VCDate, @FromTime, @ToTime, @LocationId, 'New', @CreatedBy, GETDATE())";

                            SqlCommand cmdHeader = new SqlCommand(insertHeaderQuery, conn, transaction);
                            cmdHeader.Parameters.AddWithValue("@VCId", newVCId);
                            cmdHeader.Parameters.AddWithValue("@CompanyId", ddlCompany.SelectedValue);
                            cmdHeader.Parameters.AddWithValue("@VCTypeId", ddlVCType.SelectedValue);
                            cmdHeader.Parameters.AddWithValue("@VCAccountId", ddlVCAccount.SelectedValue);
                            cmdHeader.Parameters.AddWithValue("@Topic", txtTopic.Text.Trim());
                            cmdHeader.Parameters.Add("@VCDate", SqlDbType.DateTime).Value = DateTime.Parse(txtDate.Text);
                            cmdHeader.Parameters.Add("@FromTime", SqlDbType.DateTime).Value = fullFromDateTime;
                            cmdHeader.Parameters.Add("@ToTime", SqlDbType.DateTime).Value = fullToDateTime;
                            cmdHeader.Parameters.AddWithValue("@LocationId", ddlLocation.SelectedValue);
                            cmdHeader.Parameters.AddWithValue("@CreatedBy", createdByName);
                            cmdHeader.ExecuteNonQuery();

                            foreach (string email in participantEmails)
                            {
                                SqlCommand cmdParticipant = new SqlCommand(@"
                                    INSERT INTO VCParticipants (VCId, ParticipantEmail, CreatedBy, CreatedDate)
                                    VALUES (@VCId, @ParticipantEmail, @CreatedBy, GETDATE())", conn, transaction);
                                cmdParticipant.Parameters.AddWithValue("@VCId", newVCId);
                                cmdParticipant.Parameters.AddWithValue("@ParticipantEmail", email);
                                cmdParticipant.Parameters.AddWithValue("@CreatedBy", createdByName);
                                cmdParticipant.ExecuteNonQuery();
                            }

                            var zoomService = new VCBooking.Services.ZoomService();
                            var zoomResponse = await zoomService.CreateMeetingAsync(
                                txtTopic.Text.Trim(),
                                fullFromDateTime,
                                (int)(fullToDateTime - fullFromDateTime).TotalMinutes
                            );

                            meetingId = zoomResponse.id;
                            joinUrl = zoomResponse.join_url;
                            startUrl = zoomResponse.start_url;
                            password = zoomResponse.password;

                            SqlCommand cmdUpdateZoom = new SqlCommand(@"
                                UPDATE VCRequestHeader
                                SET MeetingId=@MeetingId, JoinUrl=@JoinUrl, HostUrl=@HostUrl,
                                    MeetingPassword=@MeetingPassword, Platform='Zoom', APIStatus='Success', VCStatus='Booked'
                                WHERE VCId=@VCId", conn, transaction);
                            cmdUpdateZoom.Parameters.AddWithValue("@MeetingId", meetingId);
                            cmdUpdateZoom.Parameters.AddWithValue("@JoinUrl", joinUrl);
                            cmdUpdateZoom.Parameters.AddWithValue("@HostUrl", startUrl);
                            cmdUpdateZoom.Parameters.AddWithValue("@MeetingPassword", password);
                            cmdUpdateZoom.Parameters.AddWithValue("@VCId", newVCId);
                            cmdUpdateZoom.ExecuteNonQuery();

                            transaction.Commit();

                            // Send Emails Outside Transaction
                            try
                            {
                                var emailService = new VCBooking.Services.EmailService();
                                await emailService.SendMeetingInviteAsync(txtTopic.Text.Trim(), fullFromDateTime, 
                                    (int)(fullToDateTime - fullFromDateTime).TotalMinutes, joinUrl, password, participantEmails);
                            }
                            catch (Exception exEmail) { System.Diagnostics.Debug.WriteLine("Email Error: " + exEmail.Message); }

                            Response.Redirect("ViewRequests.aspx?success=1");
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
                AND CAST(h.VCDate AS DATE) = CAST(@NewFromTime AS DATE)
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
        }


        private async Task CleanupExpiredMeetings()
        {
            string connStr = ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();

                string query = @"
            SELECT VCId, MeetingId
            FROM VCRequestHeader
            WHERE DATEADD(MINUTE, 10, ToTime) < GETDATE()
            AND VCStatus IN ('Booked', 'Rescheduled')
            AND MeetingId IS NOT NULL";

                SqlCommand cmd = new SqlCommand(query, conn);
                SqlDataReader reader = await cmd.ExecuteReaderAsync();

                var expiredMeetings = new List<Tuple<string, string>>();

                while (await reader.ReadAsync())
                {
                    expiredMeetings.Add(new Tuple<string, string>(
                        reader["VCId"].ToString(),
                        reader["MeetingId"].ToString()
                    ));
                }

                reader.Close();

                var zoomService = new VCBooking.Services.ZoomService();

                foreach (var meeting in expiredMeetings)
                {
                    try
                    {

                        await zoomService.DeleteMeetingAsync(meeting.Item2);

                        string updateQuery = @"
                    UPDATE VCRequestHeader
                    SET VCStatus = 'Completed',
                        APIStatus = 'Deleted'
                    WHERE VCId = @VCId";

                        SqlCommand updateCmd = new SqlCommand(updateQuery, conn);
                        updateCmd.Parameters.AddWithValue("@VCId", meeting.Item1);

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