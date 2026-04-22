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
                    gvRequests.Visible = true;
                    divEmptyState.Visible = false;
                }
                else
                {
                    gvRequests.Visible = false;
                    divEmptyState.Visible = true;
                }
            }
        }

        // Row data bound — color-code the Status column with Bootstrap badges
        protected void gvRequests_RowDataBound(object sender, GridViewRowEventArgs e)
        {
            if (e.Row.RowType == DataControlRowType.DataRow)
            {
                string status = DataBinder.Eval(e.Row.DataItem, "VCStatus").ToString();

                string badgeClass = "badge bg-secondary";
                switch (status)
                {
                    case "Booked":      badgeClass = "badge bg-success"; break;
                    case "New":        badgeClass = "badge bg-primary"; break;
                    case "Rescheduled": badgeClass = "badge bg-info text-dark"; break;
                    case "Cancelled":  badgeClass = "badge bg-danger"; break;
                    case "Completed":  badgeClass = "badge bg-dark"; break;
                }

                // Status is the 10th column (index 9, 0-based)
                int statusColIndex = 9;
                e.Row.Cells[statusColIndex].Text =
                    string.Format("<span class='{0}'>{1}</span>", badgeClass, status);
            }
        }

        // Cancel button in grid — open the modal
        protected void gvRequests_RowCommand(object sender, System.Web.UI.WebControls.GridViewCommandEventArgs e)
        {
            if (e.CommandName == "CancelMeeting")
            {
                ViewState["VCId"] = e.CommandArgument.ToString();

                ClientScript.RegisterStartupScript(GetType(), "popup", "showCancelModal();", true);
            }
        }

        // 🔥 CONFIRM CANCEL
        protected async void btnConfirmCancel_Click(object sender, EventArgs e)
        {
            try
            {
                string vcId = ViewState["VCId"].ToString();
                string reason = txtCancelReason.Text;

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
                LoadRequests();
                ClientScript.RegisterStartupScript(GetType(), "msg", "alert('Meeting Cancelled Successfully');", true);
            }
            catch (Exception ex)
            {
                ClientScript.RegisterStartupScript(GetType(), "err",
                    "alert('Error cancelling meeting: " + ex.Message.Replace("'", "") + "');", true);
            }
        }

        // 🔥 UPDATE DB STATUS
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