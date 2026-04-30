using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
using System.Web.UI.WebControls;
using VCBooking.Services;

namespace VCBooking
{
    public partial class ViewRequests : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (Session["EmployeeCode"] == null)
            {
                Response.Redirect("~/Login.aspx");
            }

            if (!IsPostBack)
            {
                LoadRequests();
            }
        }

        public string GetStatusClass(string status)
        {
            switch (status)
            {
                case "Booked": return "success-subtle";
                case "Rescheduled": return "info-subtle";
                case "Cancelled": return "danger-subtle";
                case "Completed": return "dark-subtle";
                case "New": return "primary-subtle";
                default: return "secondary-subtle";
            }
        }

        protected void LoadRequests()
        {
            string connStr = ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString;
            string currentUser = Session["UserName"] != null ? Session["UserName"].ToString() : null;

            using (SqlConnection conn = new SqlConnection(connStr))
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
                    h.CreatedDate
                FROM VCRequestHeader h
                JOIN Company_Master c ON h.CompanyId = c.CompanyId
                JOIN VC_Type_Master t ON h.VCTypeId = t.VCTypeId
                JOIN VC_Account_Master a ON h.VCAccountId = a.VCAccountId
                JOIN Location_Master l ON h.LocationId = l.LocationId
                WHERE h.CreatedBy = @CreatedBy
                ORDER BY h.CreatedDate DESC";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@CreatedBy", currentUser);

                SqlDataAdapter da = new SqlDataAdapter(cmd);
                DataTable dt = new DataTable();
                da.Fill(dt);

                if (dt.Rows.Count > 0)
                {
                    gvRequests.DataSource = dt;
                    gvRequests.DataBind();
                    divGridContainer.Visible = true;
                    divEmptyState.Visible = false;
                }
                else
                {
                    divGridContainer.Visible = false;
                    divEmptyState.Visible = true;
                }
            }
        }

        // Row data bound — color-code the Status column with Bootstrap badges
        protected void gvRequests_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                // Logic removed as badge is handled in ASPX template
            }
        }

        // Cancel button in grid — open the modal
        protected void gvRequests_RowCommand(object sender, System.Web.UI.WebControls.GridViewCommandEventArgs e)
        {
        }

        // 🔥 CONFIRM CANCEL
        protected async void btnConfirmCancel_Click(object sender, EventArgs e)
        {
            try
            {
                string vcId = hfCancelVCId.Value;
                string reason = txtCancelReason.Text;
                string currentUser = Session["UserName"] != null ? Session["UserName"].ToString() : null;

                if (string.IsNullOrWhiteSpace(vcId) || !CanCurrentEmployeeCancel(vcId, currentUser))
                {
                    ClientScript.RegisterStartupScript(GetType(), "cancelBlocked",
                        "alert('This meeting cannot be cancelled.');", true);
                    return;
                }

                // 1️⃣ Get meeting details BEFORE updating DB
                var details = GetBaseMeetingDetails(vcId);

                // 2️⃣ Call the platform API to delete the meeting
                if (details != null && !string.IsNullOrEmpty(details.MeetingId))
                {
                    try
                    {
                        if (details.Platform == "Zoom")
                        {
                            var zoomService = new VCBooking.Services.ZoomService(details.VCAccountId.ToString());
                            await zoomService.DeleteMeetingAsync(details.MeetingId);
                        }
                        else if (details.Platform == "Google Meet")
                        {
                            var googleService = new VCBooking.Services.GoogleMeetService(details.VCAccountId.ToString());
                            await googleService.DeleteMeetingAsync(details.MeetingId);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("API Deletion Failed during Cancel: " + ex.Message);
                    }
                }

                // 3️⃣ Update DB
                UpdateStatusInDB(vcId, reason);

                // 4️⃣ Send email notification
                if (details != null)
                {
                    var emailService = new EmailService(details.VCAccountId.ToString());

                    int sequence = (int)DateTime.UtcNow
                        .Subtract(new DateTime(2025, 1, 1)).TotalSeconds;

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
                        sequence);
                }

                // 5️⃣ Refresh UI
                hfCancelVCId.Value = "";
                txtCancelReason.Text = "";
                LoadRequests();
                ClientScript.RegisterStartupScript(GetType(), "msg", "showCancelSuccessModal();", true);
            }
            catch (Exception ex)
            {
                ClientScript.RegisterStartupScript(GetType(), "err",
                    "alert('Error cancelling meeting: " + ex.Message.Replace("'", "") + "');", true);
            }
        }

        // 🔥 UPDATE DB STATUS
        private bool CanCurrentEmployeeCancel(string vcId, string currentUser)
        {
            if (string.IsNullOrEmpty(currentUser)) return false;

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString))
            {
                string query = @"
                SELECT COUNT(*)
                FROM VCRequestHeader
                WHERE VCId = @VCId
                  AND CreatedBy = @CreatedBy
                  AND VCStatus IN ('Booked', 'Rescheduled')";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@VCId", vcId);
                cmd.Parameters.AddWithValue("@CreatedBy", currentUser);

                con.Open();
                return (int)cmd.ExecuteScalar() > 0;
            }
        }

        private void UpdateStatusInDB(string vcId, string reason)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString))
            {
                string query = @"
                UPDATE VCRequestHeader 
                SET VCStatus = 'Cancelled',
                    CancelReason = @Reason,
                    CancelledDate = GETDATE(),
                    CancelledBy = @By
                WHERE VCId = @VCId";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Reason", reason);
                cmd.Parameters.AddWithValue("@By", Session["UserName"] != null ? Session["UserName"].ToString() : null);
                cmd.Parameters.AddWithValue("@VCId", vcId);

                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // 🔥 GET MEETING DETAILS
        private MeetingDetails GetBaseMeetingDetails(string vcId)
        {
            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString))
            {
                string query = @"
                SELECT Topic, VCDate, FromTime, ToTime, MeetingId, JoinUrl, 
                       MeetingPassword AS Password, VCAccountId, Platform 
                FROM VCRequestHeader 
                WHERE VCId = @Id";

                SqlCommand cmd = new SqlCommand(query, con);
                cmd.Parameters.AddWithValue("@Id", vcId);

                con.Open();

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        var d = new MeetingDetails
                        {
                            Topic = reader["Topic"].ToString(),
                            Date = Convert.ToDateTime(reader["VCDate"]),
                            FromTime = Convert.ToDateTime(reader["FromTime"]).TimeOfDay,
                            ToTime = Convert.ToDateTime(reader["ToTime"]).TimeOfDay,
                            MeetingId = reader["MeetingId"].ToString(),
                            JoinUrl = reader["JoinUrl"].ToString(),
                            Password = reader["Password"].ToString(),
                            VCAccountId = reader["VCAccountId"],
                            Platform = reader["Platform"].ToString(),
                            Participants = GetParticipants(vcId)
                        };

                        return d;
                    }
                }
            }

            return null;
        }

        // 🔥 GET PARTICIPANTS
        private List<string> GetParticipants(string vcId)
        {
            var list = new List<string>();

            using (SqlConnection con = new SqlConnection(ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString))
            {
                SqlCommand cmd = new SqlCommand(
                    "SELECT ParticipantEmail FROM VCParticipants WHERE VCId = @Id", con);

                cmd.Parameters.AddWithValue("@Id", vcId);

                con.Open();

                using (SqlDataReader r = cmd.ExecuteReader())
                {
                    while (r.Read())
                    {
                        list.Add(r["ParticipantEmail"].ToString());
                    }
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
            public object VCAccountId;
            public string Platform;
            public List<string> Participants;
        }
    }
}
