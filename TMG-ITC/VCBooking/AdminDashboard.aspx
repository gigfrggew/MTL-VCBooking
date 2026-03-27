<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="AdminDashboard.aspx.cs" Inherits="VCBooking.AdminDashboard" Async="true" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">

<head runat="server">
    <title>Admin Dashboard</title>
    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css" rel="stylesheet" />
    <script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js"></script>
    <script>
        function showDeleteModal(btn) {
            document.getElementById('<%= hfDeleteVCId.ClientID %>').value = btn.getAttribute("data-vcid");
            var modal = new bootstrap.Modal(document.getElementById('deleteModal'));
            modal.show();
            return false; // stop postback
        }

        function showCancelModal(btn) {
            document.getElementById('<%= hfCancelVCId.ClientID %>').value = btn.getAttribute("data-vcid");
            document.getElementById('<%= txtCancelReason.ClientID %>').value = '';
            var modal = new bootstrap.Modal(document.getElementById('cancelModal'));
            modal.show();
            return false; // stop postback
        }

        function openRescheduleModal() {
            var modal = new bootstrap.Modal(document.getElementById('rescheduleModal'));
            modal.show();
        }

        // Auto-refresh every 60 seconds to pick up status changes
        setInterval(function () {
            // Only refresh if no modal is open
            var openModals = document.querySelectorAll('.modal.show');
            if (openModals.length === 0) {
                __doPostBack('', '');
            }
        }, 60000);
    </script>
    <style>
        .btn-disabled-custom {
            opacity: 0.5;
            cursor: not-allowed !important;
            pointer-events: none;
        }

        .btn-disabled-custom:hover {
            cursor: not-allowed !important;
        }
    </style>
</head>

<body>
    <form id="form1" runat="server">
        <asp:HiddenField ID="hfDeleteVCId" runat="server" />
        <div class="container-fluid px-4 px-md-5 mt-4">
            <div class="mb-3">
                <a href="EmployeeDashboard.aspx" class="btn btn-secondary">&larr; Back to Dashboard</a>
            </div>
            <h2 class="mb-4 fw-bold text-center">Admin Dashboard</h2>
            <div class="table-responsive shadow-sm rounded">
                <asp:GridView ID="gvMeetings" runat="server" AutoGenerateColumns="False" 
                    CssClass="table table-bordered table-hover text-center align-middle"
                    OnRowCommand="gvMeetings_RowCommand"
                    OnRowDataBound="gvMeetings_RowDataBound">

                    <Columns>
                        <asp:BoundField DataField="VCId" HeaderText="VC ID" />
                        <asp:BoundField DataField="CompanyName" HeaderText="Company" />
                        <asp:BoundField DataField="VCTypeName" HeaderText="Platform" />
                        <asp:BoundField DataField="VCAccountName" HeaderText="Account" />
                        <asp:BoundField DataField="Topic" HeaderText="Topic" />
                        <asp:BoundField DataField="CreatedBy" HeaderText="Created By" />
                        <asp:BoundField DataField="VCDate" HeaderText="Date" DataFormatString="{0:dd-MMM-yyyy}" />
                        <asp:BoundField DataField="FromTime" HeaderText="From" DataFormatString="{0:hh\:mm}" />
                        <asp:BoundField DataField="ToTime" HeaderText="To" DataFormatString="{0:hh\:mm}" />
                        <asp:BoundField DataField="LocationName" HeaderText="Location" />
                        <asp:BoundField DataField="VCStatus" HeaderText="Status" />

                        <asp:TemplateField HeaderText="Action">
                            <ItemTemplate>
                                <asp:Button ID="btnReschedule" runat="server" Text="Reschedule"
                                    CommandName="RescheduleMeeting" CommandArgument='<%# Eval("VCId") %>'
                                    CssClass="btn btn-primary btn-sm" />
                                <asp:Button ID="btnCancel" runat="server" Text="Cancel" CommandName="CancelMeeting"
                                    CommandArgument='<%# Eval("VCId") %>' data-vcid='<%# Eval("VCId") %>' CssClass="btn btn-warning btn-sm"
                                    OnClientClick="return showCancelModal(this);" />
                                <asp:Button ID="btnDelete" runat="server" Text="Delete"
                                    CommandName="DeleteMeeting"
                                    CommandArgument='<%# Eval("VCId") %>' data-vcid='<%# Eval("VCId") %>'
                                    CssClass='<%# Eval("VCStatus").ToString() == "Completed" || Eval("VCStatus").ToString() == "Cancelled" ? "btn btn-danger btn-sm" : "btn btn-danger btn-sm btn-disabled-custom" %>'
                                    Enabled='<%# Eval("VCStatus").ToString() == "Completed" || Eval("VCStatus").ToString() == "Cancelled" %>'
                                    OnClientClick="return showDeleteModal(this);" />
                            </ItemTemplate>
                        </asp:TemplateField>
                    </Columns>
                </asp:GridView>

                <!-- Cancellation Modal -->
                <div class="modal fade" id="cancelModal" tabindex="-1">
                    <div class="modal-dialog modal-dialog-centered">
                        <div class="modal-content shadow">
                            <div class="modal-header bg-warning text-dark">
                                <h5 class="modal-title">Cancel Meeting</h5>
                                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                            </div>
                            <div class="modal-body text-start">
                                <asp:HiddenField ID="hfCancelVCId" runat="server" />
                                <p>Are you sure you want to cancel this meeting?</p>
                                <div class="mb-3">
                                    <label class="form-label">Reason for Cancellation:</label>
                                    <asp:TextBox ID="txtCancelReason" runat="server" TextMode="MultiLine" CssClass="form-control"
                                        Rows="3" placeholder="Enter reason..."></asp:TextBox>
                                </div>
                            </div>
                            <div class="modal-footer">
                                <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Back</button>
                                <asp:Button ID="btnConfirmCancel" runat="server" Text="Confirm Cancellation"
                                    CssClass="btn btn-warning" OnClick="btnConfirmCancel_Click"
                                    OnClientClick="this.disabled=true;this.value='Sending...';" />
                            </div>
                        </div>
                    </div>
                </div>

                <!-- Reschedule Modal -->
                <div class="modal fade" id="rescheduleModal" tabindex="-1">
                    <div class="modal-dialog modal-dialog-centered">
                        <div class="modal-content shadow">
                            <div class="modal-header bg-primary text-white">
                                <h5 class="modal-title">Reschedule Meeting</h5>
                                <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                            </div>
                            <div class="modal-body text-start">
                                <asp:HiddenField ID="hfRescheduleVCId" runat="server" />
                                <p>Proceed with rescheduling?</p>
                                <div class="row g-3">
                                    <div class="col-md-4">
                                        <label class="form-label">New Date:</label>
                                        <asp:TextBox ID="txtNewDate" runat="server" TextMode="Date" CssClass="form-control"></asp:TextBox>
                                    </div>
                                    <div class="col-md-4">
                                        <label class="form-label">New Start Time:</label>
                                        <asp:TextBox ID="txtNewFromTime" runat="server" TextMode="Time" CssClass="form-control"></asp:TextBox>
                                    </div>
                                    <div class="col-md-4">
                                        <label class="form-label">New End Time:</label>
                                        <asp:TextBox ID="txtNewToTime" runat="server" TextMode="Time" CssClass="form-control"></asp:TextBox>
                                    </div>
                                    <div class="col-12">
                                        <label class="form-label">Reason for Reschedule:</label>
                                        <asp:TextBox ID="txtRescheduleReason" runat="server" TextMode="MultiLine"
                                            CssClass="form-control" Rows="2" placeholder="Optional..."></asp:TextBox>
                                    </div>
                                </div>
                            </div>
                            <div class="modal-footer">
                                <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Back</button>
                                <asp:Button ID="btnConfirmReschedule" runat="server" Text="Confirm Reschedule"
                                    CssClass="btn btn-primary" OnClick="btnConfirmReschedule_Click"
                                    OnClientClick="this.disabled=true;this.value='Sending...';" />
                            </div>
                        </div>
                    </div>
                </div>
            </div>
            
            <!-- Delete Confirmation Modal -->
            <div class="modal fade" id="deleteModal" tabindex="-1">
                <div class="modal-dialog modal-dialog-centered">
                    <div class="modal-content shadow">
                        <div class="modal-header bg-danger text-white">
                            <h5 class="modal-title">Confirm Delete</h5>
                            <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                        </div>
                        <div class="modal-body">
                            Are you sure you want to permanently delete this meeting?
                        </div>
                        <div class="modal-footer">
                            <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
                            <asp:Button ID="btnConfirmDelete" runat="server"
                                CssClass="btn btn-danger"
                                Text="Delete"
                                OnClick="btnConfirmDelete_Click" />
                        </div>
                    </div>
                </div>
            </div>
            
        </div>
    </form>
    
</body>
</html>