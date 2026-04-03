using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Web.UI;
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
            string currentUser = Session["UserName"]?.ToString();

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

                gvRequests.DataSource = dt;
                gvRequests.DataBind();
            }
        }

        // 🔥 CANCEL BUTTON CLICK (open modal)
        protected void gvRequests_RowCommand(object sender, System.Web.UI.WebControls.GridViewCommandEventArgs e)
        {
            if (e.CommandName == "CancelMeeting")
            {
                ViewState["VCId"] = e.CommandArgument.ToString();

                ScriptManager.RegisterStartupScript(this, GetType(), "popup", "showCancelModal();", true);
            }
        }

        // 🔥 CONFIRM CANCEL
        protected async void btnConfirmCancel_Click(object sender, EventArgs e)
        {
            string vcId = ViewState["VCId"].ToString();
            string reason = txtCancelReason.Text;

            // 1️⃣ Update DB
            UpdateStatusInDB(vcId, reason);

            // 2️⃣ Get meeting details
            var details = GetBaseMeetingDetails(vcId);

            if (details != null)
            {
                // 3️⃣ Send email
                var emailService = new EmailService(details.VCAccountId.ToString());

                int sequence = (int)DateTime.UtcNow
                    .Subtract(new DateTime(2025, 1, 1)).TotalSeconds;

                string cancelledBy = Session["UserName"]?.ToString();
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
                sequence
                 );
            }

            // 4️⃣ Refresh UI
            LoadRequests();

            // Optional success alert
            ScriptManager.RegisterStartupScript(this, GetType(), "msg", "alert('Meeting Cancelled Successfully');", true);
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
                cmd.Parameters.AddWithValue("@By", Session["UserName"]?.ToString());
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
                       MeetingPassword AS Password, VCAccountId 
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
            public List<string> Participants;
        }
    }
}