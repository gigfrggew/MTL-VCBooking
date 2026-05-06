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
                Response.Redirect("~/Login.aspx", false);
                Context.ApplicationInstance.CompleteRequest();
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

                // Initialize the GridView so that EmptyDataText is rendered immediately
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

        protected void gvParticipants_RowCommand(object sender, System.Web.UI.WebControls.GridViewCommandEventArgs e)
        {
            if (e.CommandName == "RemoveParticipant")
            {
                int index = int.Parse(e.CommandArgument.ToString());
                DataTable dt = ViewState["Participants"] as DataTable;

                if (dt != null && index >= 0 && index < dt.Rows.Count)
                {
                    dt.Rows.RemoveAt(index);
                    ViewState["Participants"] = dt;

                    gvParticipants.DataSource = dt;
                    gvParticipants.DataBind();
                }
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
            LoadBookedSlots();
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
                    ShowToastMessage("Please fill all required fields", "error");
                    return;
                }

                DataTable dt = ViewState["Participants"] as DataTable;
                if (dt == null || dt.Rows.Count == 0)
                {
                    ShowToastMessage("Add at least one participant", "error");
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
                                ShowToastMessage("Please select valid duration", "error");
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
                                ShowToastMessage("Selected VC Account is already booked!", "error");
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
                                    roomUrl
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
                            string successScript = "if (typeof hideProcessingOverlay === 'function') hideProcessingOverlay(); new bootstrap.Modal(document.getElementById('successModal')).show();";
                            ScriptManager.RegisterStartupScript(this, GetType(), "showSuccessModal", successScript, true);
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
                ShowToastMessage("Error creating meeting: " + msg, "error");
            }
        }

        private void ShowToastMessage(string message, string type = "error")
        {
            string bgColor = type == "error" ? "#fee2e2" : "#dcfce3";
            string color = type == "error" ? "#ef4444" : "#22c55e";
            string icon = type == "error" ? "bi-exclamation-circle-fill" : "bi-check-circle-fill";
            
            string script = string.Format(@"
                if (typeof hideProcessingOverlay === 'function') hideProcessingOverlay();
                
                var toastHtml = `<div class='custom-toast' style='position: fixed; top: 20px; right: 20px; z-index: 1050; background: {1}; color: {2}; padding: 15px 20px; border-radius: 8px; box-shadow: 0 10px 25px rgba(0,0,0,0.1); border-left: 4px solid {2}; font-weight: 500; opacity: 0; transform: translateY(-20px); transition: all 0.3s ease; display: flex; align-items: center; gap: 10px;'><i class='bi {3} fs-5'></i> <span>{0}</span></div>`;
                document.body.insertAdjacentHTML('beforeend', toastHtml);
                var toasts = document.querySelectorAll('.custom-toast');
                var toast = toasts[toasts.length - 1];
                setTimeout(() => {{ toast.style.opacity = '1'; toast.style.transform = 'translateY(0)'; }}, 10);
                setTimeout(() => {{ toast.style.opacity = '0'; toast.style.transform = 'translateY(-20px)'; setTimeout(() => toast.remove(), 300); }}, 5000);
            ", message.Replace("'", "\\'"), bgColor, color, icon);

            ScriptManager.RegisterStartupScript(this, GetType(), "toastAlert_" + Guid.NewGuid().ToString("N"), script, true);
        }





        private void LoadAvailableAccounts()
        {
            try
            {
                string previousSelection = ddlVCAccount.SelectedValue;

                ddlVCAccount.Items.Clear();
                ddlVCAccount.Items.Add(new ListItem("-- Select Account --", ""));
                ViewState["BusyAccountIds"] = new List<string>();

                if (ddlVCType.SelectedValue == "" ||
                    string.IsNullOrEmpty(txtDate.Text) ||
                    ddlFromHour.SelectedValue == "" ||
                    ddlFromMinute.SelectedValue == "")
                    return;

                int hours = string.IsNullOrEmpty(ddlHours.SelectedValue) ? 0 : int.Parse(ddlHours.SelectedValue);
                int minutes = string.IsNullOrEmpty(ddlMinutes.SelectedValue) ? 0 : int.Parse(ddlMinutes.SelectedValue);
                int duration = (hours * 60) + minutes;

                if (duration == 0)
                {
                    ddlVCAccount.Items.Clear();
                    ddlVCAccount.Items.Add(new ListItem("-- Set a duration first --", ""));
                    return;
                }

                int fromHour = int.Parse(ddlFromHour.SelectedValue);
                int fromMinute = int.Parse(ddlFromMinute.SelectedValue);
                DateTime newFrom = DateTime.Parse(txtDate.Text).AddHours(fromHour).AddMinutes(fromMinute);
                DateTime newTo = newFrom.AddMinutes(duration);

                string connStr = ConfigurationManager.ConnectionStrings["HRConnection"].ConnectionString;
                var busyIds = new List<string>();

                using (SqlConnection conn = new SqlConnection(connStr))
                {
                    // Fetch ALL active accounts for this VC type,
                    // with a flag (IsBusy) indicating if they clash with the selected slot.
                    string query = @"
                        SELECT
                            a.VCAccountId,
                            a.VCAccountName,
                            CASE WHEN EXISTS (
                                SELECT 1 FROM VCRequestHeader h
                                WHERE h.VCAccountId = a.VCAccountId
                                AND h.VCStatus NOT IN ('Cancelled', 'Completed')
                                AND (@NewFrom < h.ToTime AND @NewTo > h.FromTime)
                            ) THEN 1 ELSE 0 END AS IsBusy
                        FROM VC_Account_Master a
                        WHERE a.VCTypeId = @VCTypeId
                        AND a.Status = 'Active'
                        ORDER BY IsBusy ASC, a.VCAccountName ASC";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@VCTypeId", ddlVCType.SelectedValue);
                    cmd.Parameters.Add("@NewFrom", SqlDbType.DateTime).Value = newFrom;
                    cmd.Parameters.Add("@NewTo", SqlDbType.DateTime).Value = newTo;

                    conn.Open();
                    SqlDataReader reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        string id = reader["VCAccountId"].ToString();
                        string name = reader["VCAccountName"].ToString();
                        bool isBusy = reader["IsBusy"].ToString() == "1";

                        string displayName = isBusy ? name + "  ⚠ Already Booked" : name;
                        var item = new ListItem(displayName, id);

                        if (isBusy)
                        {
                            busyIds.Add(id);
                        }

                        ddlVCAccount.Items.Add(item);
                    }
                }

                ViewState["BusyAccountIds"] = busyIds;

                // Restore previous selection if it still exists
                if (!string.IsNullOrEmpty(previousSelection) && ddlVCAccount.Items.FindByValue(previousSelection) != null)
                {
                    ddlVCAccount.SelectedValue = previousSelection;

                    if (busyIds.Contains(previousSelection))
                    {
                        // Show toast and reset selection
                        string accountName = ddlVCAccount.SelectedItem.Text.Replace("  ⚠ Already Booked", "").Trim();
                        string msg = string.Format("{0} is already booked for the selected time slot. Please choose a different account or adjust the time.", accountName);
                        
                        string script = string.Format(@"
                            var toastHtml = `<div id='busyToast' style='position: fixed; top: 20px; right: 20px; z-index: 1050; background: #fee2e2; color: #ef4444; padding: 15px 20px; border-radius: 8px; box-shadow: 0 10px 25px rgba(0,0,0,0.1); border-left: 4px solid #ef4444; font-weight: 500; opacity: 0; transform: translateY(-20px); transition: all 0.3s ease; display: flex; align-items: center; gap: 10px;'><i class='bi bi-exclamation-circle-fill fs-5'></i> <span>{0}</span></div>`;
                            document.body.insertAdjacentHTML('beforeend', toastHtml);
                            var toast = document.getElementById('busyToast');
                            setTimeout(() => {{ toast.style.opacity = '1'; toast.style.transform = 'translateY(0)'; }}, 10);
                            setTimeout(() => {{ toast.style.opacity = '0'; toast.style.transform = 'translateY(-20px)'; setTimeout(() => toast.remove(), 300); }}, 10000);
                        ", msg.Replace("'", "\\'"));

                        ScriptManager.RegisterStartupScript(this, GetType(), "busyAlert", script, true);
                        ddlVCAccount.SelectedIndex = 0; // reset to --Select--
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Error loading accounts: " + ex.Message);
                ddlVCAccount.Items.Clear();
                ddlVCAccount.Items.Add(new ListItem("-- Error Loading Accounts --", ""));
            }
        }

        protected void ddlVCAccount_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedId = ddlVCAccount.SelectedValue;
            if (string.IsNullOrEmpty(selectedId)) return;

            var busyIds = ViewState["BusyAccountIds"] as List<string>;

            if (busyIds != null && busyIds.Contains(selectedId))
            {
                // Show toast and reset selection
                string accountName = ddlVCAccount.SelectedItem.Text.Replace("  ⚠ Already Booked", "").Trim();
                string msg = string.Format("{0} is already booked for the selected time slot. Please choose a different account or adjust the time.", accountName);
                
                string script = string.Format(@"
                    var toastHtml = `<div id='busyToast' style='position: fixed; top: 20px; right: 20px; z-index: 1050; background: #fee2e2; color: #ef4444; padding: 15px 20px; border-radius: 8px; box-shadow: 0 10px 25px rgba(0,0,0,0.1); border-left: 4px solid #ef4444; font-weight: 500; opacity: 0; transform: translateY(-20px); transition: all 0.3s ease; display: flex; align-items: center; gap: 10px;'><i class='bi bi-exclamation-circle-fill fs-5'></i> <span>{0}</span></div>`;
                    document.body.insertAdjacentHTML('beforeend', toastHtml);
                    var toast = document.getElementById('busyToast');
                    setTimeout(() => {{ toast.style.opacity = '1'; toast.style.transform = 'translateY(0)'; }}, 10);
                    setTimeout(() => {{ toast.style.opacity = '0'; toast.style.transform = 'translateY(-20px)'; setTimeout(() => toast.remove(), 300); }}, 10000);
                ", msg.Replace("'", "\\'"));

                ScriptManager.RegisterStartupScript(this, GetType(), "busyAlert", script, true);
                ddlVCAccount.SelectedIndex = 0; // reset to --Select--
                return;
            }

            LoadBookedSlots();
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

            int selectedVCTypeId = 0;
            bool hasVCTypeFilter = int.TryParse(ddlVCType.SelectedValue, out selectedVCTypeId);
            bool hasDateFilter = !string.IsNullOrEmpty(txtDate.Text);

            int hours = string.IsNullOrEmpty(ddlHours.SelectedValue) ? 0 : int.Parse(ddlHours.SelectedValue);
            int minutes = string.IsNullOrEmpty(ddlMinutes.SelectedValue) ? 0 : int.Parse(ddlMinutes.SelectedValue);
            int duration = (hours * 60) + minutes;

            bool hasTimeFilter = (duration > 0 && !string.IsNullOrEmpty(ddlFromHour.SelectedValue) && !string.IsNullOrEmpty(ddlFromMinute.SelectedValue));
            DateTime? newFrom = null;
            DateTime? newTo = null;

            if (hasTimeFilter && hasDateFilter)
            {
                int fromHour = int.Parse(ddlFromHour.SelectedValue);
                int fromMinute = int.Parse(ddlFromMinute.SelectedValue);
                newFrom = DateTime.Parse(txtDate.Text).AddHours(fromHour).AddMinutes(fromMinute);
                newTo = newFrom.Value.AddMinutes(duration);
            }

            bool hasAccountFilter = !string.IsNullOrEmpty(ddlVCAccount.SelectedValue);

            if (!hasVCTypeFilter)
            {
                gvBookedSlots.DataSource = null;
                gvBookedSlots.DataBind();
                return;
            }

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
                    AND (@HasDateFilter = 0 OR CAST(h.VCDate AS DATE) = CAST(@SelectedDate AS DATE))
                    AND (@HasVCTypeFilter = 0 OR h.VCTypeId = @VCTypeId)
                    AND (@HasAccountFilter = 0 OR h.VCAccountId = @VCAccountId)
                    ORDER BY h.FromTime";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.Add("@HasDateFilter", SqlDbType.Bit).Value = hasDateFilter;
                cmd.Parameters.Add("@SelectedDate", SqlDbType.Date).Value = hasDateFilter ? (object)DateTime.Parse(txtDate.Text) : DBNull.Value;
                cmd.Parameters.Add("@HasVCTypeFilter", SqlDbType.Bit).Value = hasVCTypeFilter;
                cmd.Parameters.Add("@VCTypeId", SqlDbType.Int).Value = hasVCTypeFilter ? (object)selectedVCTypeId : DBNull.Value;
                
                cmd.Parameters.Add("@HasAccountFilter", SqlDbType.Bit).Value = hasAccountFilter;
                cmd.Parameters.Add("@VCAccountId", SqlDbType.Int).Value = hasAccountFilter ? (object)int.Parse(ddlVCAccount.SelectedValue) : DBNull.Value;

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
            AND VCStatus IN ('Booked', 'Rescheduled')";

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

                        string apiStatus = "DeleteSkipped";

                        if (!string.IsNullOrWhiteSpace(meetingId))
                        {
                            try
                            {
                                if (platform.IndexOf("Zoom", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    var zoomService = new VCBooking.Services.ZoomService(vcAccountId);
                                    await zoomService.DeleteMeetingAsync(meetingId);
                                    apiStatus = "Deleted";
                                }
                                else if (platform.IndexOf("Google", StringComparison.OrdinalIgnoreCase) >= 0)
                                {
                                    var googleService = new VCBooking.Services.GoogleMeetService(vcAccountId);
                                    await googleService.DeleteMeetingAsync(meetingId);
                                    apiStatus = "Deleted";
                                }
                            }
                            catch (Exception ex)
                            {
                                apiStatus = "DeleteFailed";
                                System.Diagnostics.Debug.WriteLine("Cleanup API delete error: " + ex.Message);
                            }
                        }

                        string updateQuery = @"
                    UPDATE VCRequestHeader
                    SET VCStatus = 'Completed',
                        APIStatus = @APIStatus
                    WHERE VCId = @VCId";

                        SqlCommand updateCmd = new SqlCommand(updateQuery, conn);
                        updateCmd.Parameters.AddWithValue("@VCId", vcId);
                        updateCmd.Parameters.AddWithValue("@APIStatus", apiStatus);

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
