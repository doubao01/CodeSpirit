<%@ Page %>

<cs:Content PlaceHolder="Head">
  <link rel="stylesheet" href="/css/pages/admin.css" />
</cs:Content>

<cs:Content PlaceHolder="Body">
  <div class="admin-page">
    <section class="admin-hero">
      <div>
        <p class="eyebrow">Backend Management</p>
        <h1>Library Admin</h1>
        <p>Manage books, readers, circulation, reservations, overdue loans, and fines from one MVVM page.</p>
      </div>
      <a class="screen-link" href="/">Back to Dashboard</a>
    </section>

    <cs:Region Name="admin-metrics" Tag="section" class="admin-metrics">
      <cs:Repeater Items="{Binding Metrics}">
        <article class="admin-metric metric-{Binding Tone}">
          <span>{Binding Label}</span>
          <strong>{Binding Value}</strong>
          <small>{Binding Hint}</small>
        </article>
      </cs:Repeater>
    </cs:Region>

    <cs:Form class="search-card">
      <cs:Field Name="Query" Label="Search" Placeholder="Title, author, or ISBN" />
      <cs:Field Name="FilterStatus" Label="Status" Placeholder="All, Available, Borrowed, Reserved, Overdue, Archived" />
      <cs:Field Name="FilterCategory" Label="Category" Placeholder="All or category name" />
      <cs:Button Command="Search">Apply Filter</cs:Button>
    </cs:Form>

    <cs:Region Name="admin-notices" Tag="section" class="notice-row">
      <cs:Conditional Visible="{Binding Notices}">
        <cs:Repeater Items="{Binding Notices}">
          <div class="notice notice-{Binding Tone}">{Binding Text}</div>
        </cs:Repeater>
      </cs:Conditional>
    </cs:Region>

    <section class="admin-grid admin-grid-wide">
      <cs:Form class="admin-card">
        <h2>Book Catalog</h2>
        <p>Add a book, or provide Book Id to update/archive/restore existing records.</p>
        <cs:Field Name="BookId" Label="Book Id" Required="true" ValidateWhen="UpdateBook, ArchiveBook, RestoreBook" />
        <cs:Field Name="BookIsbn" Label="ISBN" MaxLength="20" ValidateWhen="AddBook, UpdateBook" />
        <cs:Field Name="BookTitle" Label="Title" Required="true" MaxLength="120" ValidateWhen="AddBook, UpdateBook" />
        <cs:Field Name="BookAuthor" Label="Author" />
        <cs:Field Name="BookCategory" Label="Category" />
        <cs:Field Name="BookLocation" Label="Location" />
        <cs:Field Name="BookPublishedYear" Label="Year" Type="number" />
        <cs:Field Name="BookCopyCount" Label="Copies" Type="number" Min="1" Max="999" ValidateWhen="AddBook" />
        <div class="button-row wrap">
          <cs:Button Command="AddBook">Add</cs:Button>
          <cs:Button Command="UpdateBook">Update</cs:Button>
          <cs:Button Command="ArchiveBook">Archive</cs:Button>
          <cs:Button Command="RestoreBook">Restore</cs:Button>
        </div>
      </cs:Form>

      <cs:Form class="admin-card">
        <h2>Readers</h2>
        <p>Register readers, update profiles, and control active/suspended status.</p>
        <cs:Field Name="ReaderId" Label="Reader Id" />
        <cs:Field Name="ReaderName" Label="Name" Required="true" MaxLength="40" ValidateWhen="RegisterReader, UpdateReader" />
        <cs:Field Name="ReaderEmail" Label="Email" Type="email" ValidateWhen="RegisterReader, UpdateReader" />
        <cs:Field Name="ReaderPhone" Label="Phone" Type="tel" ValidateWhen="RegisterReader, UpdateReader" />
        <cs:Field Name="ReaderLevel" Label="Level" Placeholder="Standard, Premium, Student" />
        <div class="button-row wrap">
          <cs:Button Command="RegisterReader">Register</cs:Button>
          <cs:Button Command="UpdateReader">Update</cs:Button>
          <cs:Button Command="SuspendReader">Suspend</cs:Button>
          <cs:Button Command="ActivateReader">Activate</cs:Button>
        </div>
      </cs:Form>

      <cs:Form class="admin-card">
        <h2>Circulation</h2>
        <p>Borrow by Book Id and Reader Id. Return or renew by Loan Id.</p>
        <cs:Field Name="BookId" Label="Book Id" />
        <cs:Field Name="LoanReaderId" Label="Reader Id" />
        <cs:Field Name="LoanId" Label="Loan Id" />
        <div class="button-row wrap">
          <cs:Button Command="BorrowBook">Borrow</cs:Button>
          <cs:Button Command="ReturnBook">Return</cs:Button>
          <cs:Button Command="RenewLoan">Renew</cs:Button>
        </div>
      </cs:Form>

      <cs:Form class="admin-card">
        <h2>Reservations and Fines</h2>
        <p>Queue reservations, cancel waiting reservations, and collect reader fines.</p>
        <cs:Field Name="BookId" Label="Book Id" />
        <cs:Field Name="ReservationReaderId" Label="Reader Id" />
        <cs:Field Name="ReservationId" Label="Reservation Id" />
        <cs:Field Name="ReaderId" Label="Fine Reader Id" />
        <cs:Field Name="FineAmount" Label="Fine Amount" Type="number" Required="true" Min="0" Max="10000" ValidateWhen="CollectFine" />
        <div class="button-row wrap">
          <cs:Button Command="ReserveBook">Reserve</cs:Button>
          <cs:Button Command="CancelReservation">Cancel Reservation</cs:Button>
          <cs:Button Command="CollectFine">Collect Fine</cs:Button>
        </div>
      </cs:Form>

      <article class="admin-card activity-card">
        <h2>Activity</h2>
        <cs:Repeater Items="{Binding Activities}">
          <div class="activity activity-{Binding Tone}">
            <time>{Binding Time}</time>
            <span>{Binding Text}</span>
          </div>
        </cs:Repeater>
      </article>

      <cs:Form class="admin-card inventory-card">
        <h2>Inventory Control</h2>
        <p>Receive new copies, write off damaged copies, and relocate collection items with audit history.</p>
        <cs:Field Name="InventoryBookId" Label="Book Id" />
        <cs:Field Name="InventoryQuantity" Label="Quantity" Type="number" />
        <cs:Field Name="InventoryLocation" Label="New Location" Placeholder="A-01, B-12, Archive" />
        <cs:Field Name="InventoryReason" Label="Reason" Placeholder="Purchase order, damaged copy, shelf optimization" />
        <div class="button-row wrap">
          <cs:Button Command="ReceiveCopies">Receive</cs:Button>
          <cs:Button Command="WriteOffCopies">Write Off</cs:Button>
          <cs:Button Command="RelocateBook">Relocate</cs:Button>
        </div>
      </cs:Form>

      <cs:Form class="admin-card import-export-card">
        <h2>CSV Import and Export</h2>
        <p>Export the current catalog filter, edit the CSV, and import it back. Existing ISBN values are updated, new ISBN values are added.</p>
        <cs:Field Name="ImportExportCsv" Label="CSV Workspace" Type="textarea" Rows="9" Placeholder="ISBN,Title,Author,Category,Location,PublishedYear,CopyCount,Rating" />
        <div class="button-row wrap">
          <cs:Button Command="ExportBooks">Export Filtered Books</cs:Button>
          <cs:Button Command="ImportBooks">Import CSV</cs:Button>
          <cs:Button Command="ClearImportExport">Clear</cs:Button>
        </div>
      </cs:Form>
    </section>

    <cs:Region Name="collection-table" Tag="section" class="book-table-card">
      <div class="panel-title">
        <h2>Collection</h2>
        <span>Filtered catalog with stock, reservation, and due-date context</span>
      </div>
      <div class="table-scroll">
        <cs:Table Items="{Binding Books}">
          <cs:Column Header="Id">{Binding Id}</cs:Column>
          <cs:Column Header="ISBN">{Binding Isbn}</cs:Column>
          <cs:Column Header="Title">{Binding Title}</cs:Column>
          <cs:Column Header="Author">{Binding Author}</cs:Column>
          <cs:Column Header="Category">{Binding Category}</cs:Column>
          <cs:Column Header="Status"><span class="status status-{Binding Status}">{Binding Status}</span></cs:Column>
          <cs:Column Header="Copies">{Binding AvailableCopies}/{Binding CopyCount}</cs:Column>
          <cs:Column Header="Borrower">{Binding Borrower}</cs:Column>
          <cs:Column Header="Reserved By">{Binding ReservedBy}</cs:Column>
          <cs:Column Header="Due">{Binding DueDate}</cs:Column>
          <cs:Column Header="Location">{Binding Location}</cs:Column>
        </cs:Table>
      </div>
    </cs:Region>

    <cs:Region Name="reader-loan-tables" Tag="section" class="split-tables">
      <article class="book-table-card">
        <div class="panel-title"><h2>Readers</h2><span>Active loans, reservations, and fines</span></div>
        <div class="table-scroll">
          <cs:Table Items="{Binding Readers}" Columns="Id:Id,Name:Name,Level:Level,Status:Status,ActiveLoans:Loans,Reservations:Reservations,FineBalance:Fine,Email:Email" />
        </div>
      </article>

      <article class="book-table-card">
        <div class="panel-title"><h2>Loans</h2><span>Borrowing, renewal, return, and overdue state</span></div>
        <div class="table-scroll">
          <cs:Table Items="{Binding Loans}" Columns="Id:Loan,BookTitle:Book,ReaderName:Reader,BorrowedAt:Borrowed,DueAt:Due,ReturnedAt:Returned,Status:Status,RenewCount:Renew,Fine:Fine" />
        </div>
      </article>
    </cs:Region>

    <cs:Region Name="reservation-table" Tag="section" class="book-table-card">
      <div class="panel-title"><h2>Reservations</h2><span>Waiting, completed, and cancelled reservations</span></div>
      <div class="table-scroll">
        <cs:Table Items="{Binding Reservations}" Columns="Id:Id,BookTitle:Book,ReaderName:Reader,CreatedAt:Created,Status:Status" />
      </div>
    </cs:Region>

    <cs:Region Name="inventory-table" Tag="section" class="book-table-card">
      <div class="panel-title"><h2>Inventory Audit</h2><span>Inbound, write-off, and relocation events</span></div>
      <div class="table-scroll">
        <cs:Table Items="{Binding InventoryEvents}" Columns="Id:Id,BookTitle:Book,Type:Type,Quantity:Qty,Reason:Reason,CreatedAt:Created" />
      </div>
    </cs:Region>
  </div>
</cs:Content>
