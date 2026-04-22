using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Web.UI;
using System.Web.UI.WebControls;
using VCBooking.Services;

namespace VCBooking
{
    public partial class AdminDashboard : System.Web.UI.Page
    {
        protected async void Page_Load(object sender, EventArgs e)
        {
            if (Session["EmployeeCode"] == null)
            {
                Response.Redirect("~/Login.aspx");
            }
            if (Session["IsAdmin"] == null || (bool)Session["IsAdmin"] == false)
            {
                Response.Redirect("~/Dashboard.aspx");
            }
            if (!IsPostBack)
            {
                await CleanupExpiredMeetings();
                LoadMeetings();
            }
        }

        /// <summary>
        /// Auto-cleanup: Mark expired Booked/Rescheduled meetings as Completed
        /// and delete Zoom meeting (DB entry stays).
        /// Runs 10 min after ToTime.
        /// </summary>
        private async Task CleanupExpiredMeetings()
        {
            try
            {
                using (SqlConnection con = new SqlConnection(
                    ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString))
                {
                    await con.OpenAsync();

                    // ✅ STEP 1: Include VCAccountId and Platform
                    string query = @"
                SELECT VCId, MeetingId, VCAccountId, Platform
                FROM VCRequestHeader
                WHERE DATEADD(MINUTE, 10, ToTime) < GETDATE()
                AND VCStatus IN ('Booked', 'Rescheduled')
                AND MeetingId IS NOT NULL";

                    SqlCommand cmd = new SqlCommand(query, con);
                    SqlDataReader reader = await cmd.ExecuteReaderAsync();

                    // ✅ STEP 2: Store VCAccountId and Platform
                    var expiredMeetings = new List<Tuple<string, string, string, string>>();

                    while (await reader.ReadAsync())
                    {
                        expiredMeetings.Add(new Tuple<string, string, string, string>(
                            reader["VCId"].ToString(),          // Item1
                            reader["MeetingId"].ToString(),     // Item2
                            reader["VCAccountId"].ToString(),    // Item3
                            reader["Platform"].ToString()       // Item4
                        ));
                    }

                    reader.Close();

                    // ❌ REMOVE this line (wrong)
                    // var zoomService = new Services.ZoomService(vcAccountId);

                    // ✅ STEP 3: Create ZoomService per meeting
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
                                var zoomService = new Services.ZoomService(vcAccountId);
                                await zoomService.DeleteMeetingAsync(meetingId);
                            }
                            else if (platform == "Google Meet")
                            {
                                var googleService = new Services.GoogleMeetService(vcAccountId);
                                await googleService.DeleteMeetingAsync(meetingId);
                            }

                            string updateQuery = @"
                        UPDATE VCRequestHeader
                        SET VCStatus = 'Completed',
                        APIStatus = 'Deleted'
                        WHERE VCId = @VCId";

                            SqlCommand updateCmd = new SqlCommand(updateQuery, con);
                            updateCmd.Parameters.AddWithValue("@VCId", vcId);

                            await updateCmd.ExecuteNonQueryAsync();
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine("Cleanup error: " + ex.Message);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("CleanupExpiredMeetings error: " + ex.Message);
            }
        }



        private void LoadMeetings()
        {
            using (SqlConnection con = new SqlConnection(
                ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString))
            {
                string query = @"
                 SELECT 
                 h.VCId,
                 c.CompanyName,
                 t.VCTypeName,
                 a.VCAccountName,
                 h.Topic,
                 h.VCDate,
                 h.FromTime,
                 h.ToTime,
                 l.LocationName,
                 h.VCStatus,
                 h.CreatedBy
                 FROM VCRequestHeader h
                 JOIN Company_Master c ON h.CompanyId = c.CompanyId
                 JOIN VC_Type_Master t ON h.VCTypeId = t.VCTypeId
                 JOIN VC_Account_Master a ON h.VCAccountId = a.VCAccountId
                 JOIN Location_Master l ON h.LocationId = l.LocationId
                 ORDER BY h.CreatedDate DESC";

                SqlDataAdapter da = new SqlDataAdapter(query, con);
                DataTable dt = new DataTable();
                da.Fill(dt);

                gvMeetings.DataSource = dt;
                gvMeetings.DataBind();
            }
        }

        protected void gvMeetings_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                string status = DataBinder.Eval(e.Row.DataItem, "VCStatus").ToString();
                bool isTerminal = (status == "Cancelled" || status == "Completed");

                // Disable Reschedule & Cancel for terminal statuses
                Button btnReschedule = (Button)e.Row.FindControl("btnReschedule");
                Button btnCancel = (Button)e.Row.FindControl("btnCancel");

                if (btnReschedule != null)
                {
                    btnReschedule.Enabled = !isTerminal;
                    if (isTerminal)
                        btnReschedule.CssClass = "btn btn-primary btn-sm btn-disabled-custom";
                }
                if (btnCancel != null)
                {
                    btnCancel.Enabled = !isTerminal;
                    if (isTerminal)
                        btnCancel.CssClass = "btn btn-warning btn-sm btn-disabled-custom";
                }

                // Status badge styling
                int statusIndex = 10; // VCStatus column index
                string badgeClass = "badge bg-secondary";
                switch (status)
                {
                    case "Booked": badgeClass = "badge bg-success"; break;
                    case "Rescheduled": badgeClass = "badge bg-info text-dark"; break;
                    case "Cancelled": badgeClass = "badge bg-danger"; break;
                    case "Completed": badgeClass = "badge bg-dark"; break;
                    case "New": badgeClass = "badge bg-primary"; break;
                }
                e.Row.Cells[statusIndex].Text = string.Format("<span class='{0}'>{1}</span>", badgeClass, status);
            }
        }

        private void SoftDeleteMeeting(string vcId, string reason)
        {
            using (SqlConnection con = new SqlConnection(
                ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString))
            {
                string query = @"UPDATE VCRequestHeader
                                 SET VCStatus = 'Cancelled',
                                     CancelledBy = @CancelledBy,
                                     CancelledDate = GETDATE(),
                                     CancelReason = @CancelReason
                                 WHERE VCId = @VCId";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@VCId", vcId);

                string cancelledBy = Session["Username"] != null
                                     ? Session["Username"].ToString()
                                     : "Admin";

                cmd.Parameters.AddWithValue("@CancelledBy", cancelledBy);
                cmd.Parameters.AddWithValue("@CancelReason", reason);

                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        protected async void gvMeetings_RowCommand(object sender, GridViewCommandEventArgs e)
        {
            string vcId = e.CommandArgument.ToString();

            if (e.CommandName == "RescheduleMeeting")
            {
                hfRescheduleVCId.Value = vcId;
                LoadMeetingDetailsForReschedule(vcId);
                ClientScript.RegisterStartupScript(this.GetType(), "ShowReschedule", "openRescheduleModal();", true);
            }
            else if (e.CommandName == "DeleteMeeting")
            {
                // Handled by btnConfirmDelete_Click and the Bootstrap modal
            }
        }

        private void LoadMeetingDetailsForReschedule(string vcId)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString))
            {
                string query = "SELECT VCDate, FromTime, ToTime FROM VCRequestHeader WHERE VCId = @VCId";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@VCId", vcId);
                con.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        txtNewDate.Text = Convert.ToDateTime(reader["VCDate"]).ToString("yyyy-MM-dd");
                        txtNewFromTime.Text = Convert.ToDateTime(reader["FromTime"]).ToString("HH:mm");
                        txtNewToTime.Text = Convert.ToDateTime(reader["ToTime"]).ToString("HH:mm");
                    }
                }
            }
        }

        protected async void btnConfirmCancel_Click(object sender, EventArgs e)
        {
            try
            {
                string vcId = hfCancelVCId.Value;
                string reason = txtCancelReason.Text;

                // 1. Get Meeting Details
                var details = GetBaseMeetingDetails(vcId);
                if (details == null) return;

                // 2. Delete Meeting via API
                if (!string.IsNullOrEmpty(details.MeetingId))
                {
                    try
                    {
                        string platform = details.Platform;
                        if (platform == "Zoom")
                        {
                            var zoom = new Services.ZoomService(details.VCAccountId.ToString());
                            await zoom.DeleteMeetingAsync(details.MeetingId);
                        }
                        else if (platform == "Google Meet")
                        {
                            var google = new Services.GoogleMeetService(details.VCAccountId.ToString());
                            await google.DeleteMeetingAsync(details.MeetingId);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("API Deletion Failed during Cancel: " + ex.Message);
                    }
                }

                // 3. Update DB
                UpdateStatusInDB(vcId, "Cancelled", reason);

                // 4. Notify participants
                var emailService = new EmailService(details.VCAccountId.ToString());
                int sequence = (int)DateTime.UtcNow.Subtract(new DateTime(2025, 1, 1)).TotalSeconds;
                string cancelledBy = Session["UserName"] != null ? Session["UserName"].ToString() : null;
                bool isAdmin = Session["IsAdmin"] != null && (bool)Session["IsAdmin"];

                await emailService.SendCancellationNotificationAsync(
                details.Topic,
                details.Date,
                details.FromTime,
                details.ToTime,
                details.MeetingId,
                reason,
                details.Participants,
                cancelledBy, 
                isAdmin,       
                details.Platform,
                sequence      
                 );

                txtCancelReason.Text = "";
                LoadMeetings();
            }
            catch (Exception ex)
            {
                Response.Write("<script>alert('Error: " + ex.Message.Replace("'", "") + "');</script>");
            }
        }

        protected async void btnConfirmReschedule_Click(object sender, EventArgs e)
        {
            try
            {
                string vcId = hfRescheduleVCId.Value;
                string reason = txtRescheduleReason.Text;
                DateTime newDate = DateTime.Parse(txtNewDate.Text);
                TimeSpan newFrom = TimeSpan.Parse(txtNewFromTime.Text);
                TimeSpan newTo = TimeSpan.Parse(txtNewToTime.Text);

                // 1. Get Details
                var details = GetBaseMeetingDetails(vcId);
                if (details == null) return;

                // 1.5. Overlap Check
                if (details.VCAccountId != null) // We need AccountId for check
                {
                    bool isOverlapping = IsAccountBusy(details.VCAccountId.ToString(), newDate, newFrom, newTo, vcId);
                    if (isOverlapping)
                    {
                        Response.Write("<script>alert('Error: The selected account is already booked for this time slot.');</script>");
                        return;
                    }
                }

                // 2. Update Meeting via API
                if (!string.IsNullOrEmpty(details.MeetingId))
                {
                    DateTime start = newDate.Add(newFrom);
                    int duration = (int)(newTo - newFrom).TotalMinutes;
                    string platform = details.Platform;

                    if (platform == "Zoom")
                    {
                        var zoom = new Services.ZoomService(details.VCAccountId.ToString());
                        await zoom.UpdateMeetingAsync(details.MeetingId, details.Topic, start, duration);
                    }
                    else if (platform == "Google Meet")
                    {
                        var google = new Services.GoogleMeetService(details.VCAccountId.ToString());
                        await google.UpdateMeetingAsync(details.MeetingId, details.Topic, start, (int)(newTo - newFrom).TotalMinutes);
                    }
                }

                // 3. Update DB
                UpdateScheduleInDB(vcId, newDate, newFrom, newTo);

                // 4. Notify
                var emailService = new EmailService(details.VCAccountId.ToString());
                int sequence = (int)DateTime.UtcNow.Subtract(new DateTime(2025, 1, 1)).TotalSeconds;
                await emailService.SendRescheduleNotificationAsync(
                    details.Topic,
                    details.Date, details.FromTime, details.ToTime,
                    newDate, newFrom, newTo,
                    details.JoinUrl, details.MeetingId, details.Password,
                    reason, details.Participants, details.Platform, sequence);

                LoadMeetings();
            }
            catch (Exception ex)
            {
                Response.Write("<script>alert('Error: " + ex.Message.Replace("'", "") + "');</script>");
            }
        }

        protected async void btnConfirmDelete_Click(object sender, EventArgs e)
        {
            try
            {
                string vcId = hfDeleteVCId.Value != null ? hfDeleteVCId.Value.Trim() : "";
                if (string.IsNullOrEmpty(vcId)) return;

                // Re-check status before delete (backend validation)
                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString))
                {
                    string statusQuery = "SELECT VCStatus FROM VCRequestHeader WHERE VCId = @Id";
                    SqlCommand cmd = new SqlCommand(statusQuery, con);
                    cmd.Parameters.AddWithValue("@Id", vcId);
                    con.Open();
                    object result = cmd.ExecuteScalar();
                    string status = result != null ? result.ToString().Trim() : null;

                    if (status != "Completed" && status != "Cancelled")
                    {
                        Response.Write("<script>alert('Error: Only Completed or Cancelled meetings can be deleted. (VCId: " + vcId + ", Status: " + (status ?? "NULL") + ")');</script>");
                        return; // prevent unsafe delete
                    }
                }

                await DeleteMeetingPermanently(vcId);
                LoadMeetings();
            }
            catch (Exception ex)
            {
                Response.Write("<script>alert('Error: " + ex.Message.Replace("'", "") + "');</script>");
            }
        }

        private void UpdateStatusInDB(string vcId, string status, string reason)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString))
            {
                string query = @"
                UPDATE VCRequestHeader 
                SET VCStatus = @Status,
                APIStatus = @Status,  
                CancelReason = @Reason, 
                CancelledDate = GETDATE(), 
                CancelledBy = @By 
                WHERE VCId = @VCId";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Status", status);
                cmd.Parameters.AddWithValue("@Reason", reason);
                cmd.Parameters.AddWithValue("@By", Session["UserName"] != null ? Session["UserName"].ToString() : "Admin");
                cmd.Parameters.AddWithValue("@VCId", vcId);

                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        private void UpdateScheduleInDB(string vcId, DateTime date, TimeSpan from, TimeSpan to)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString))
            {
                string query = "UPDATE VCRequestHeader SET VCDate = @Date, FromTime = @From, ToTime = @To, VCStatus = 'Rescheduled' WHERE VCId = @VCId";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Date", date.Date);
                cmd.Parameters.Add("@From", SqlDbType.DateTime).Value = date.Date.Add(from);
                cmd.Parameters.Add("@To", SqlDbType.DateTime).Value = date.Date.Add(to);
                cmd.Parameters.AddWithValue("@VCId", vcId);
                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        private async Task DeleteMeetingPermanently(string vcId)
        {
            try
            {
                var details = GetBaseMeetingDetails(vcId);
                if (details != null && !string.IsNullOrEmpty(details.MeetingId))
                {
                    try
                    {
                        string platform = details.Platform;
                        if (platform == "Zoom")
                        {
                            await new Services.ZoomService(details.VCAccountId.ToString())
                                .DeleteMeetingAsync(details.MeetingId);
                        }
                        else if (platform == "Google Meet")
                        {
                            await new Services.GoogleMeetService(details.VCAccountId.ToString())
                                .DeleteMeetingAsync(details.MeetingId);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("API Deletion Failed: " + ex.Message);
                    }
                }

                using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString))
                {
                    con.Open();
                    using (SqlTransaction trans = con.BeginTransaction())
                    {
                        try
                        {
                            // ✅ STEP 1: Update status to 'Deleted'
                            SqlCommand cmdUpdate = new SqlCommand(@"
                        UPDATE VCRequestHeader
                        SET VCStatus = 'Deleted',
                            APIStatus = 'Deleted'
                        WHERE VCId = @Id", con, trans);

                            cmdUpdate.Parameters.AddWithValue("@Id", vcId);
                            cmdUpdate.ExecuteNonQuery();

                            // ✅ STEP 2: Delete participants
                            SqlCommand cmdP = new SqlCommand("DELETE FROM VCParticipants WHERE VCId = @Id", con, trans);
                            cmdP.Parameters.AddWithValue("@Id", vcId);
                            cmdP.ExecuteNonQuery();

                            // ✅ STEP 3: Delete main record
                            SqlCommand cmdH = new SqlCommand("DELETE FROM VCRequestHeader WHERE VCId = @Id", con, trans);
                            cmdH.Parameters.AddWithValue("@Id", vcId);
                            cmdH.ExecuteNonQuery();

                            trans.Commit();
                        }
                        catch
                        {
                            trans.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Response.Write("<script>alert('Delete failed: " + ex.Message.Replace("'", "") + "');</script>");
            }
        }

        private bool IsAccountBusy(string accountId, DateTime date, TimeSpan from, TimeSpan to, string excludeVcId)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString))
            {
                string query = @"SELECT COUNT(*) FROM VCRequestHeader 
                                 WHERE VCAccountId = @AccountId 
                                 AND VCStatus NOT IN ('Cancelled', 'Completed')
                                 AND VCId <> @ExcludeId
                                 AND (@Start < ToTime AND @End > FromTime)";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@AccountId", accountId);
                cmd.Parameters.AddWithValue("@ExcludeId", excludeVcId);

                DateTime start = date.Add(from);
                DateTime end = date.Add(to);
                cmd.Parameters.Add("@Start", SqlDbType.DateTime).Value = start;
                cmd.Parameters.Add("@End", SqlDbType.DateTime).Value = end;

                con.Open();
                int count = (int)cmd.ExecuteScalar();
                return count > 0;
            }
        }

        private MeetingDetails GetBaseMeetingDetails(string vcId)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString))
            {
                string query = "SELECT Topic, VCDate, FromTime, ToTime, MeetingId, JoinUrl, MeetingPassword AS Password, VCAccountId, Platform FROM VCRequestHeader WHERE VCId = @Id";
                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Id", vcId);
                con.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var d = new MeetingDetails();
                        d.Topic = reader["Topic"].ToString();
                        d.Date = Convert.ToDateTime(reader["VCDate"]);
                        d.FromTime = Convert.ToDateTime(reader["FromTime"]).TimeOfDay;
                        d.ToTime = Convert.ToDateTime(reader["ToTime"]).TimeOfDay;
                        d.MeetingId = reader["MeetingId"].ToString();
                        d.JoinUrl = reader["JoinUrl"].ToString();
                        d.Password = reader["Password"].ToString();
                        d.VCAccountId = reader["VCAccountId"];
                        d.Platform = reader["Platform"].ToString();
                        d.Participants = GetParticipants(vcId);
                        return d;
                    }
                }
            }
            return null;
        }

        private List<string> GetParticipants(string vcId)
        {
            var list = new List<string>();
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString))
            {
                SqlCommand cmd = new SqlCommand("SELECT ParticipantEmail FROM VCParticipants WHERE VCId = @Id", con);
                cmd.Parameters.AddWithValue("@Id", vcId);
                con.Open();
                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read()) list.Add(r["ParticipantEmail"].ToString());
                }
            }
            return list;
        }



        private class MeetingDetails
        {
            public string Topic;
            public DateTime Date;
            public TimeSpan FromTime;
            public TimeSpan ToTime;
            public string MeetingId;
            public string JoinUrl;
            public string Password;
            public string Platform;
            public object VCAccountId;
            public List<string> Participants;
        }
    }
}