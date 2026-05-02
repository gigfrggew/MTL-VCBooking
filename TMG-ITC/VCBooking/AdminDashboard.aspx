<%@ Page Language="C#" AutoEventWireup="true" CodeBehind="AdminDashboard.aspx.cs" Inherits="VCBooking.AdminDashboard"
    Async="true" %>

    <!DOCTYPE html>
    <html xmlns="http://www.w3.org/1999/xhtml">

    <head runat="server">
        <meta charset="utf-8" />
        <meta name="viewport" content="width=device-width, initial-scale=1.0" />
        <title>Admin Dashboard</title>
        <link href="../Content/bootstrap.min.css" rel="stylesheet" />
        <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css" />
        <link href="../Content/Site.css?v=20260427-clean3" rel="stylesheet" />
        <script src="../Scripts/bootstrap.bundle.min.js"></script>
        <script>
            function showDeleteModal(btn) {
                document.getElementById('<%= hfDeleteVCId.ClientID %>').value = btn.getAttribute("data-vcid");
                var modal = new bootstrap.Modal(document.getElementById('deleteModal'));
                modal.show();
                return false;
            }

            function showCancelModal(btn) {
                document.getElementById('<%= hfCancelVCId.ClientID %>').value = btn.getAttribute("data-vcid");
                document.getElementById('<%= txtCancelReason.ClientID %>').value = '';
                var modal = new bootstrap.Modal(document.getElementById('cancelModal'));
                modal.show();
                return false;
            }

            function openRescheduleModal() {
                var modal = new bootstrap.Modal(document.getElementById('rescheduleModal'));
                modal.show();
            }

            function loadAdminMeetingDetails(vcId) {
                document.getElementById('<%= hfDetailsVCId.ClientID %>').value = vcId;
                document.getElementById('<%= btnLoadDetails.ClientID %>').click();
            }

            function openDetailsModal() {
                var modal = new bootstrap.Modal(document.getElementById('detailsModal'));
                modal.show();
            }

            function stopRowDetails(event) {
                event.stopPropagation();
            }

            setInterval(function () {
                var openModals = document.querySelectorAll('.modal.show');
                if (openModals.length === 0) {
                    __doPostBack('', '');
                }
            }, 60000);
        </script>
    </head>

    <body>
        <form id="form1" runat="server">
            <asp:ScriptManager ID="ScriptManager1" runat="server" />
            <asp:HiddenField ID="hfDeleteVCId" runat="server" />
            <asp:HiddenField ID="hfDetailsVCId" runat="server" />
            <asp:Button ID="btnLoadDetails" runat="server" OnClick="btnLoadDetails_Click" Style="display:none;" UseSubmitBehavior="false" />

            <div class="rail-viewport">
                <div class="rail-card premium-card">

                    <div class="page-header-row card-header-minimal">
                        <h2 class="card-title-premium m-0">Admin Dashboard</h2>
                        <a href="EmployeeDashboard.aspx" class="nav-back-link"><i class="bi bi-arrow-left"></i> Dashboard</a>
                    </div>

                    <div class="admin-filter-row">
                        <div class="admin-filter-field">
                            <asp:Label ID="lblVCTypeFilter" runat="server" AssociatedControlID="ddlVCTypeFilter"
                                CssClass="text-label" Text="Filter by VC Type" />
                            <asp:DropDownList ID="ddlVCTypeFilter" runat="server"
                                AutoPostBack="true"
                                CssClass="form-select form-control-premium" />
                        </div>
                        <div class="admin-filter-summary">
                            <asp:Literal ID="litMeetingFilterSummary" runat="server" />
                        </div>
                    </div>

                    <div class="table-responsive-premium">
                        <asp:GridView ID="gvMeetings" runat="server" AutoGenerateColumns="False"
                            CssClass="table table-premium compact-table text-center align-middle"
                            OnRowCommand="gvMeetings_RowCommand" OnRowDataBound="gvMeetings_RowDataBound"
                            GridLines="None">

                            <Columns>
                                <asp:BoundField DataField="VCId" HeaderText="VC ID" />
                                <asp:BoundField DataField="CompanyName" HeaderText="Company" />
                                <asp:BoundField DataField="VCTypeName" HeaderText="Platform" />
                                <asp:BoundField DataField="Topic" HeaderText="Topic" />
                                <asp:BoundField DataField="CreatedBy" HeaderText="Created By" />
                                <asp:BoundField DataField="VCDate" HeaderText="Date" DataFormatString="{0:dd-MMM-yyyy}" />
                                <asp:TemplateField HeaderText="Time">
                                    <ItemTemplate>
                                        <%# Convert.ToDateTime(Eval("FromTime")).ToString("hh:mm tt") %> - <%# Convert.ToDateTime(Eval("ToTime")).ToString("hh:mm tt") %>
                                    </ItemTemplate>
                                </asp:TemplateField>

                                <asp:TemplateField HeaderText="Status">
                                    <ItemTemplate>
                                        <span class='<%# "badge-subtle badge-" + GetStatusClass(Eval("VCStatus").ToString()) %>'>
                                            <%# Eval("VCStatus") %>
                                        </span>
                                    </ItemTemplate>
                                </asp:TemplateField>

                                <asp:TemplateField HeaderText="Actions">
                                    <ItemTemplate>
                                        <div class="d-flex gap-2 justify-content-center" onclick="stopRowDetails(event);">
                                            <asp:Button ID="btnReschedule" runat="server" Text="Reschedule"
                                                CommandName="RescheduleMeeting" CommandArgument='<%# Eval("VCId") %>'
                                                CssClass="btn btn-primary-premium btn-sm btn-premium" />

                                            <asp:Button ID="btnCancel" runat="server" Text="Cancel"
                                                data-vcid='<%# Eval("VCId") %>'
                                                CssClass="btn btn-outline-warning btn-sm btn-premium"
                                                UseSubmitBehavior="false" CausesValidation="false"
                                                OnClientClick="return showCancelModal(this);" />

                                            <asp:Button ID="btnDelete" runat="server" Text="Delete" CommandName="DeleteMeeting"
                                                CommandArgument='<%# Eval("VCId") %>' data-vcid='<%# Eval("VCId") %>'
                                                CssClass='<%# (Eval("VCStatus").ToString() == "Completed" || Eval("VCStatus").ToString() == "Cancelled") ? "btn btn-outline-danger btn-sm btn-premium" : "btn btn-outline-danger btn-sm btn-premium disabled" %>'
                                                Enabled='<%# Eval("VCStatus").ToString() == "Completed" || Eval("VCStatus").ToString() == "Cancelled" %>'
                                                OnClientClick="return showDeleteModal(this);" />
                                        </div>
                                    </ItemTemplate>
                                </asp:TemplateField>
                            </Columns>
                        </asp:GridView>
                    </div>

                    <!-- Meeting Details Modal -->
                    <div class="modal fade details-modal" id="detailsModal" tabindex="-1">
                        <div class="modal-dialog modal-dialog-centered modal-xl">
                            <div class="modal-content modal-content-premium">
                                <div class="modal-header modal-header-minimal">
                                    <div>
                                        <div class="text-label mb-1">Meeting Details</div>
                                        <h5 class="modal-title fw-bold mb-0">
                                            <asp:Literal ID="litDetailTitle" runat="server" />
                                        </h5>
                                    </div>
                                    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                                </div>
                                <div class="modal-body modal-body-premium">
                                    <asp:Panel ID="pnlDetailsEmpty" runat="server" Visible="false" CssClass="empty-state py-4">
                                        <div class="empty-icon"><i class="bi bi-search"></i></div>
                                        <h2>Meeting Not Found</h2>
                                        <p class="mb-0">The selected meeting could not be loaded.</p>
                                    </asp:Panel>

                                    <asp:Panel ID="pnlDetailsContent" runat="server">
                                        <div class="details-grid">
                                            <div class="detail-item"><span>VC ID</span><strong><asp:Literal ID="litDetailVCId" runat="server" /></strong></div>
                                            <div class="detail-item"><span>Status</span><strong><asp:Literal ID="litDetailStatus" runat="server" /></strong></div>
                                            <div class="detail-item"><span>Company</span><strong><asp:Literal ID="litDetailCompany" runat="server" /></strong></div>
                                            <div class="detail-item"><span>Platform</span><strong><asp:Literal ID="litDetailPlatform" runat="server" /></strong></div>
                                            <div class="detail-item"><span>Account</span><strong><asp:Literal ID="litDetailAccount" runat="server" /></strong></div>
                                            <div class="detail-item"><span>Location</span><strong><asp:Literal ID="litDetailLocation" runat="server" /></strong></div>
                                            <div class="detail-item"><span>Date</span><strong><asp:Literal ID="litDetailDate" runat="server" /></strong></div>
                                            <div class="detail-item"><span>Time</span><strong><asp:Literal ID="litDetailTime" runat="server" /></strong></div>
                                            <div class="detail-item"><span>Created By</span><strong><asp:Literal ID="litDetailCreatedBy" runat="server" /></strong></div>
                                            <div class="detail-item"><span>Created Date</span><strong><asp:Literal ID="litDetailCreatedDate" runat="server" /></strong></div>
                                            <div class="detail-item"><span>Participants</span><strong><asp:Literal ID="litDetailParticipantCount" runat="server" /></strong></div>
                                            <div class="detail-item"><span>API Status</span><strong><asp:Literal ID="litDetailApiStatus" runat="server" /></strong></div>
                                            <div class="detail-item detail-wide"><span>Unit / Floor</span><strong><asp:Literal ID="litDetailUnitFloor" runat="server" /></strong></div>
                                            <div class="detail-item detail-wide"><span>VC Details</span><strong><asp:Literal ID="litDetailVCDetails" runat="server" /></strong></div>
                                            <div class="detail-item"><span>Meeting ID</span><strong><asp:Literal ID="litDetailMeetingId" runat="server" /></strong></div>
                                            <div class="detail-item"><span>Password</span><strong><asp:Literal ID="litDetailPassword" runat="server" /></strong></div>
                                            <div class="detail-item detail-wide"><span>Join URL</span><strong><asp:HyperLink ID="lnkDetailJoinUrl" runat="server" Target="_blank" /></strong></div>
                                            <div class="detail-item detail-wide"><span>Participants</span><strong><asp:Literal ID="litDetailParticipants" runat="server" /></strong></div>
                                            <div class="detail-item detail-wide"><span>Cancellation</span><strong><asp:Literal ID="litDetailCancellation" runat="server" /></strong></div>
                                        </div>

                                        <div class="details-actions">
                                            <asp:Button ID="btnDetailsReschedule" runat="server" Text="Reschedule"
                                                CssClass="btn btn-primary-premium btn-premium"
                                                OnClick="btnDetailsReschedule_Click" />
                                            <asp:Button ID="btnDetailsCancel" runat="server" Text="Cancel Meeting"
                                                CssClass="btn btn-outline-warning btn-premium"
                                                OnClick="btnDetailsCancel_Click" />
                                            <asp:Button ID="btnDetailsDelete" runat="server" Text="Delete"
                                                CssClass="btn btn-outline-danger btn-premium"
                                                OnClick="btnDetailsDelete_Click" />
                                        </div>
                                    </asp:Panel>
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- Cancellation Modal -->
                    <div class="modal fade" id="cancelModal" tabindex="-1">
                        <div class="modal-dialog modal-dialog-centered">
                            <div class="modal-content modal-content-premium">
                                <div class="modal-header modal-header-minimal">
                                    <h5 class="modal-title fw-bold">Cancel Meeting</h5>
                                    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                                </div>
                                <div class="modal-body modal-body-premium">
                                    <asp:HiddenField ID="hfCancelVCId" runat="server" />
                                    <p class="text-secondary">Are you sure you want to cancel this meeting? This action can't be undone.</p>
                                    <div class="mb-3">
                                        <label class="form-label fw-semibold">Reason for Cancellation:</label>
                                        <asp:TextBox ID="txtCancelReason" runat="server" TextMode="MultiLine"
                                            CssClass="form-control form-control-premium" Rows="3" placeholder="Explain why...">
                                        </asp:TextBox>
                                    </div>
                                    <div class="d-flex gap-2 justify-content-end mt-4">
                                        <button type="button" class="btn btn-light btn-premium" data-bs-dismiss="modal">Back</button>
                                        <asp:Button ID="btnConfirmCancel" runat="server" Text="Confirm Cancellation"
                                            CssClass="btn btn-danger btn-premium" OnClick="btnConfirmCancel_Click"
                                            UseSubmitBehavior="false"
                                            OnClientClick="var btn=this; setTimeout(function(){ btn.disabled=true; btn.value='Processing...'; },10);" />
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- Reschedule Modal -->
                    <div class="modal fade" id="rescheduleModal" tabindex="-1">
                        <div class="modal-dialog modal-dialog-centered modal-lg">
                            <div class="modal-content modal-content-premium">
                                <div class="modal-header modal-header-minimal">
                                    <h5 class="modal-title fw-bold">Reschedule Meeting</h5>
                                    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                                </div>
                                <div class="modal-body modal-body-premium">
                                    <asp:HiddenField ID="hfRescheduleVCId" runat="server" />
                                    <p class="text-secondary mb-4">Please select the new time slot for the meeting.</p>
                                    <div class="row g-4">
                                        <div class="col-md-4">
                                            <label class="form-label fw-semibold">New Date</label>
                                            <asp:TextBox ID="txtNewDate" runat="server" TextMode="Date"
                                                CssClass="form-control form-control-premium"></asp:TextBox>
                                        </div>
                                        <div class="col-md-4">
                                            <label class="form-label fw-semibold">New Start Time</label>
                                            <asp:TextBox ID="txtNewFromTime" runat="server" TextMode="Time"
                                                CssClass="form-control form-control-premium"></asp:TextBox>
                                        </div>
                                        <div class="col-md-4">
                                            <label class="form-label fw-semibold">New End Time</label>
                                            <asp:TextBox ID="txtNewToTime" runat="server" TextMode="Time"
                                                CssClass="form-control form-control-premium"></asp:TextBox>
                                        </div>
                                        <div class="col-12">
                                            <label class="form-label fw-semibold">Reason for Reschedule</label>
                                            <asp:TextBox ID="txtRescheduleReason" runat="server" TextMode="MultiLine"
                                                CssClass="form-control form-control-premium" Rows="2" placeholder="Add a note for participants...">
                                            </asp:TextBox>
                                        </div>
                                    </div>
                                    <div class="d-flex gap-2 justify-content-end mt-4">
                                        <button type="button" class="btn btn-light btn-premium" data-bs-dismiss="modal">Back</button>
                                        <asp:Button ID="btnConfirmReschedule" runat="server" Text="Confirm Reschedule"
                                            CssClass="btn btn-primary-premium btn-premium" OnClick="btnConfirmReschedule_Click"
                                            UseSubmitBehavior="false"
                                            OnClientClick="var btn=this; setTimeout(function(){ btn.disabled=true; btn.value='Processing...'; },10);" />
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                    <!-- Delete Confirmation Modal -->
                    <div class="modal fade" id="deleteModal" tabindex="-1">
                        <div class="modal-dialog modal-dialog-centered">
                            <div class="modal-content modal-content-premium">
                                <div class="modal-header modal-header-minimal">
                                    <h5 class="modal-title fw-bold">Confirm Delete</h5>
                                    <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
                                </div>
                                <div class="modal-body modal-body-premium">
                                    <p>Are you sure you want to permanently delete this meeting? This record will be removed from the system.</p>
                                    <div class="d-flex gap-2 justify-content-end mt-4">
                                        <button type="button" class="btn btn-light btn-premium" data-bs-dismiss="modal">Cancel</button>
                                        <asp:Button ID="btnConfirmDelete" runat="server" CssClass="btn btn-danger btn-premium" Text="Delete Permanently"
                                            OnClick="btnConfirmDelete_Click" />
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>

                </div>
            </div>
        </form>
    </body>
</html>
