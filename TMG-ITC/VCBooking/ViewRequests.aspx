<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="ViewRequests.aspx.cs" Inherits="VCBooking.ViewRequests" Async="true" %>

<!DOCTYPE html>
<html xmlns="http://www.w3.org/1999/xhtml">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>My VC Requests - VC Booking</title>
    <link href="../Content/bootstrap.min.css" rel="stylesheet" />
    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css" />
    <link href="../Content/Site.css?v=20260427-clean3" rel="stylesheet" />
    <script src="../Scripts/bootstrap.bundle.min.js"></script>
</head>
<body class="bg-ice">
    <form id="form1" runat="server">
        <asp:HiddenField ID="hfCancelVCId" runat="server" />
        <div class="rail-viewport d-flex justify-content-center align-items-center">
            <div class="rail-card premium-card" style="max-width: 1250px; width: 100%; min-height: 500px;">
                
                <div class="page-header-row">
                    <a href="EmployeeDashboard.aspx" class="nav-back-link">
                        <i class="bi bi-arrow-left"></i> Back to Menu
                    </a>
                    <h2 class="fw-bold m-0" style="color: var(--text-main);">My VC Requests</h2>
                </div>

                <!-- Grid View for Requests -->
                <div id="divGridContainer" runat="server">
                    <div class="table-responsive-premium">
                        <asp:GridView ID="gvRequests" runat="server"
                            CssClass="table table-premium compact-table text-center align-middle mb-0"
                            AutoGenerateColumns="false"
                            OnRowCommand="gvRequests_RowCommand"
                            OnRowDataBound="gvRequests_RowDataBound"
                            GridLines="None">
                            <Columns>
                                <asp:BoundField DataField="VCId" HeaderText="VC ID" />
                                <asp:BoundField DataField="CompanyName" HeaderText="Company" />
                                <asp:BoundField DataField="VCTypeName" HeaderText="Platform" />
                                <asp:BoundField DataField="Topic" HeaderText="Topic" />
                                <asp:BoundField DataField="VCDate" HeaderText="Date" DataFormatString="{0:dd-MMM-yyyy}" />
                                <asp:BoundField DataField="FromTime" HeaderText="From" DataFormatString="{0:hh:mm tt}" />
                                <asp:TemplateField HeaderText="Status">
                                    <ItemTemplate>
                                        <span class='<%# "badge-subtle badge-" + GetStatusClass(Eval("VCStatus").ToString()) %>'>
                                            <%# Eval("VCStatus") %>
                                        </span>
                                    </ItemTemplate>
                                </asp:TemplateField>
                                <asp:TemplateField HeaderText="Action">
                                    <ItemTemplate>
                                        <asp:Button ID="btnCancel" runat="server"
                                            Text="Cancel"
                                            CssClass="btn btn-outline-danger btn-sm action-button"
                                            data-vcid='<%# Eval("VCId") %>'
                                            UseSubmitBehavior="false"
                                            CausesValidation="false"
                                            OnClientClick="return showCancelModal(this);"
                                            Visible='<%# Eval("VCStatus").ToString() == "Booked" || Eval("VCStatus").ToString() == "Rescheduled" %>' />
                                    </ItemTemplate>
                                </asp:TemplateField>
                            </Columns>
                        </asp:GridView>
                    </div>
                </div>

                <!-- Empty State -->
                <div id="divEmptyState" runat="server" visible="false" class="empty-state">
                    <div class="empty-icon">
                        <i class="bi bi-calendar"></i>
                    </div>
                    <h2 class="fw-bold mb-2">No Meeting Requests Found</h2>
                    <p class="text-muted mb-4">You haven't made any video conference requests yet.</p>
                    <a href="CreateVCRequest.aspx" class="btn-dark-navy text-decoration-none">Create New Request</a>
                </div>

            </div>
        </div>

        <!-- Cancel Modal -->
        <div class="modal fade" id="cancelModal" tabindex="-1">
            <div class="modal-dialog modal-dialog-centered">
                <div class="modal-content modal-content-premium">
                    <div class="modal-header modal-header-minimal">
                        <h5 class="modal-title fw-bold">Cancel Meeting</h5>
                        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                    </div>
                    <div class="modal-body modal-body-premium">
                        <p class="text-secondary">Are you sure you want to cancel this meeting? All participants will be notified.</p>
                        <div class="mb-3">
                            <label class="form-label fw-semibold">Reason for Cancellation:</label>
                            <asp:TextBox ID="txtCancelReason" runat="server"
                                CssClass="form-control form-control-premium"
                                TextMode="MultiLine"
                                Rows="3"
                                placeholder="Optional: Let participants know why..."></asp:TextBox>
                        </div>
                        <div class="d-flex gap-2 justify-content-end mt-4">
                            <button type="button" class="btn btn-light" data-bs-dismiss="modal">Keep Meeting</button>
                            <asp:Button ID="btnConfirmCancel" runat="server"
                                Text="Yes, Cancel Meeting"
                                CssClass="btn btn-danger"
                                OnClick="btnConfirmCancel_Click" />
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <div class="modal fade" id="cancelSuccessModal" tabindex="-1">
            <div class="modal-dialog modal-dialog-centered">
                <div class="modal-content modal-content-premium">
                    <div class="modal-body modal-body-premium text-center py-5">
                        <div class="success-icon">
                            <i class="bi bi-check-lg"></i>
                        </div>
                        <h3 class="fw-bold mb-2">Meeting Cancelled</h3>
                        <p class="text-secondary mb-0">Your meeting was cancelled and participants were notified.</p>
                    </div>
                </div>
            </div>
        </div>
    </form>
    
    <script>
        function showCancelModal(btn) {
            document.getElementById('<%= hfCancelVCId.ClientID %>').value = btn.getAttribute('data-vcid');
            var modal = new bootstrap.Modal(document.getElementById('cancelModal'));
            modal.show();
            return false;
        }

        function showCancelSuccessModal() {
            var modal = new bootstrap.Modal(document.getElementById('cancelSuccessModal'));
            modal.show();
        }
    </script>
</body>
</html>
