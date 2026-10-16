using $safeprojectname$.Features.Library.Models;
using System.Globalization;
using System.Text;
using ServiceAttribute = CodeSpirit.Core.Attributes.ServiceAttribute;
using CodeSpiritServiceLifetime = CodeSpirit.Core.Attributes.ServiceLifetime;
using CodeSpirit.Core.Mvvm;

namespace $safeprojectname$.Features.Library.Services;

[Service(Lifetime = CodeSpiritServiceLifetime.Singleton)]
public class LibraryService
{
    private const string AllFilter = "All";
    private static readonly TimeProvider Clock = TimeProvider.System;

    private static class BookStatus
    {
        public const string Available = "Available";
        public const string Borrowed = "Borrowed";
        public const string Reserved = "Reserved";
        public const string Archived = "Archived";
        public const string Overdue = "Overdue";
    }

    private static class ReaderStatus
    {
        public const string Active = "Active";
        public const string Suspended = "Suspended";
    }

    private static class LoanStatus
    {
        public const string Active = "Active";
        public const string Returned = "Returned";
        public const string Overdue = "Overdue";
    }

    private static class ReservationStatus
    {
        public const string Waiting = "Waiting";
        public const string Completed = "Completed";
        public const string Cancelled = "Cancelled";
    }

    private readonly object _sync = new();
    private readonly List<BookItem> _books = [];
    private readonly List<ReaderItem> _readers = [];
    private readonly List<LoanRecord> _loans = [];
    private readonly List<ReservationItem> _reservations = [];
    private readonly List<InventoryEvent> _inventoryEvents = [];
    private readonly List<LibraryActivity> _activities = [];
    private int _nextBookId = 1;
    private int _nextReaderId = 1;
    private int _nextLoanId = 1;
    private int _nextReservationId = 1;
    private int _nextInventoryEventId = 1;

    private sealed record BookImport(string Isbn, string Title, string Author, string Category, string Location, int PublishedYear, int CopyCount, decimal Rating);

    public LibraryService()
    {
        Seed();
    }

    public LibrarySnapshot GetSnapshot(string query = "", string status = AllFilter, string category = AllFilter)
    {
        lock (_sync)
        {
            RecalculateBooks();
            RecalculateReaders();

            var books = FilterBooks(query, status, category).Select(Clone).ToList();
            var readers = _readers.Select(Clone).ToList();
            var loans = _loans.OrderByDescending(loan => loan.Id).Select(Clone).ToList();
            var reservations = _reservations.OrderByDescending(item => item.Id).Select(Clone).ToList();
            var inventoryEvents = _inventoryEvents.OrderByDescending(item => item.Id).Take(12).Select(Clone).ToList();
            var totalCopies = _books.Sum(book => book.CopyCount);
            var availableCopies = _books.Sum(book => book.AvailableCopies);
            var activeLoans = _loans.Count(IsOpenLoan);
            var overdueLoans = _loans.Count(loan => loan.Status == LoanStatus.Overdue);
            var waitingReservations = _reservations.Count(IsWaitingReservation);
            var fines = _readers.Sum(reader => reader.FineBalance);

            return new LibrarySnapshot(
                books,
                readers,
                loans,
                reservations,
                [
                    new("Books", _books.Count.ToString(), $"{totalCopies} total copies", ToneConstants.Blue),
                    new("Available", availableCopies.ToString(), "Copies ready to borrow", ToneConstants.Green),
                    new("Active Loans", activeLoans.ToString(), $"{overdueLoans} overdue", overdueLoans > 0 ? ToneConstants.Red : ToneConstants.Amber),
                    new("Reservations", waitingReservations.ToString(), $"Fines ${fines:0.00}", ToneConstants.Purple)
                ],
                _books.GroupBy(book => book.Category)
                    .Select(group => new CategoryStat(group.Key, group.Count(), group.Count(book => book.Status == BookStatus.Borrowed), group.Sum(book => book.AvailableCopies)))
                    .OrderByDescending(stat => stat.Total)
                    .ToList(),
                _books.OrderByDescending(book => book.MonthlyBorrows).Take(5).Select(Clone).ToList(),
                inventoryEvents,
                _activities.Take(8).ToList());
        }
    }

    public AdminNotice AddBook(string isbn, string title, string author, string category, string location, int publishedYear, int copyCount)
    {
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(author))
            return new("Title and author are required.", ToneConstants.Red);

        lock (_sync)
        {
            var copies = Math.Max(1, copyCount);
            var book = new BookItem
            {
                Id = _nextBookId++,
                Isbn = string.IsNullOrWhiteSpace(isbn) ? $"ISBN-{Clock.GetLocalNow().LocalDateTime:yyyyMMddHHmmss}" : isbn.Trim(),
                Title = title.Trim(),
                Author = author.Trim(),
                Category = string.IsNullOrWhiteSpace(category) ? "General" : category.Trim(),
                Location = string.IsNullOrWhiteSpace(location) ? "New Shelf" : location.Trim(),
                CopyCount = copies,
                AvailableCopies = copies,
                PublishedYear = publishedYear <= 0 ? DateTime.Today.Year : publishedYear,
                MonthlyBorrows = 0,
                Rating = 4.0m,
                LastActionAt = Clock.GetLocalNow().LocalDateTime
            };
            _books.Add(book);
            RecalculateBooks();
            AddActivity($"New book added: {book.Title}", ToneConstants.Green);
            return new($"Added {book.Title} with {copies} copies.", ToneConstants.Green);
        }
    }

    public AdminNotice UpdateBook(int bookId, string isbn, string title, string author, string category, string location, int publishedYear, int copyCount)
    {
        lock (_sync)
        {
            var book = FindBook(bookId);
            if (book is null)
                return new("Book was not found.", ToneConstants.Red);

            var activeLoans = ActiveLoansFor(bookId).Count();
            book.Isbn = string.IsNullOrWhiteSpace(isbn) ? book.Isbn : isbn.Trim();
            book.Title = string.IsNullOrWhiteSpace(title) ? book.Title : title.Trim();
            book.Author = string.IsNullOrWhiteSpace(author) ? book.Author : author.Trim();
            book.Category = string.IsNullOrWhiteSpace(category) ? book.Category : category.Trim();
            book.Location = string.IsNullOrWhiteSpace(location) ? book.Location : location.Trim();
            book.PublishedYear = publishedYear <= 0 ? book.PublishedYear : publishedYear;
            book.CopyCount = Math.Max(activeLoans, copyCount <= 0 ? book.CopyCount : copyCount);
            book.LastActionAt = Clock.GetLocalNow().LocalDateTime;
            RecalculateBooks();
            SyncLoanBookTitles(book);
            SyncReservationBookTitles(book);
            AddActivity($"Book updated: {book.Title}", ToneConstants.Blue);
            return new($"Updated {book.Title}.", ToneConstants.Blue);
        }
    }

    public AdminNotice ArchiveBook(int bookId)
    {
        lock (_sync)
        {
            var book = FindBook(bookId);
            if (book is null)
                return new("Book was not found.", ToneConstants.Red);

            if (ActiveLoansFor(bookId).Any())
                return new("Return all active loans before archiving this book.", ToneConstants.Amber);

            book.Status = BookStatus.Archived;
            book.AvailableCopies = 0;
            book.LastActionAt = Clock.GetLocalNow().LocalDateTime;
            CancelReservationsForBook(bookId);
            AddActivity($"Book archived: {book.Title}", ToneConstants.Purple);
            return new($"Archived {book.Title}.", ToneConstants.Purple);
        }
    }

    public AdminNotice RestoreBook(int bookId)
    {
        lock (_sync)
        {
            var book = FindBook(bookId);
            if (book is null)
                return new("Book was not found.", ToneConstants.Red);

            book.Status = BookStatus.Available;
            book.LastActionAt = Clock.GetLocalNow().LocalDateTime;
            RecalculateBooks();
            AddActivity($"Book restored: {book.Title}", ToneConstants.Green);
            return new($"Restored {book.Title}.", ToneConstants.Green);
        }
    }

    public AdminNotice RegisterReader(string name, string email, string phone, string level)
    {
        if (string.IsNullOrWhiteSpace(name))
            return new("Reader name is required.", ToneConstants.Red);

        lock (_sync)
        {
            var reader = new ReaderItem
            {
                Id = _nextReaderId++,
                Name = name.Trim(),
                Email = email.Trim(),
                Phone = phone.Trim(),
                Level = string.IsNullOrWhiteSpace(level) ? "Standard" : level.Trim(),
                Status = ReaderStatus.Active
            };
            _readers.Add(reader);
            AddActivity($"Reader registered: {reader.Name}", ToneConstants.Green);
            return new($"Registered reader {reader.Name}.", ToneConstants.Green);
        }
    }

    public AdminNotice UpdateReader(int readerId, string name, string email, string phone, string level)
    {
        lock (_sync)
        {
            var reader = FindReader(readerId);
            if (reader is null)
                return new("Reader was not found.", ToneConstants.Red);

            reader.Name = string.IsNullOrWhiteSpace(name) ? reader.Name : name.Trim();
            reader.Email = string.IsNullOrWhiteSpace(email) ? reader.Email : email.Trim();
            reader.Phone = string.IsNullOrWhiteSpace(phone) ? reader.Phone : phone.Trim();
            reader.Level = string.IsNullOrWhiteSpace(level) ? reader.Level : level.Trim();
            SyncLoanReaderNames(reader);
            SyncReservationReaderNames(reader);
            AddActivity($"Reader updated: {reader.Name}", ToneConstants.Blue);
            return new($"Updated reader {reader.Name}.", ToneConstants.Blue);
        }
    }

    public AdminNotice SuspendReader(int readerId)
    {
        lock (_sync)
        {
            var reader = FindReader(readerId);
            if (reader is null)
                return new("Reader was not found.", ToneConstants.Red);

            if (_loans.Any(loan => loan.ReaderId == readerId && IsOpenLoan(loan)))
                return new("Return active loans before suspending this reader.", ToneConstants.Amber);

            reader.Status = ReaderStatus.Suspended;
            CancelReservationsForReader(readerId);
            AddActivity($"Reader suspended: {reader.Name}", ToneConstants.Purple);
            return new($"Suspended reader {reader.Name}.", ToneConstants.Purple);
        }
    }

    public AdminNotice ActivateReader(int readerId)
    {
        lock (_sync)
        {
            var reader = FindReader(readerId);
            if (reader is null)
                return new("Reader was not found.", ToneConstants.Red);

            reader.Status = ReaderStatus.Active;
            AddActivity($"Reader activated: {reader.Name}", ToneConstants.Green);
            return new($"Activated reader {reader.Name}.", ToneConstants.Green);
        }
    }

    public AdminNotice BorrowBook(int bookId, int readerId)
    {
        lock (_sync)
        {
            var book = FindBook(bookId);
            var reader = FindReader(readerId);
            if (book is null)
                return new("Book was not found.", ToneConstants.Red);
            if (reader is null)
                return new("Reader was not found.", ToneConstants.Red);
            if (reader.Status != ReaderStatus.Active)
                return new("Reader must be active before borrowing.", ToneConstants.Amber);
            if (book.Status == BookStatus.Archived)
                return new("Archived books cannot be borrowed.", ToneConstants.Amber);
            if (book.AvailableCopies <= 0)
                return new("No available copies. Create a reservation instead.", ToneConstants.Amber);
            if (_loans.Any(loan => loan.BookId == bookId && loan.ReaderId == readerId && IsOpenLoan(loan)))
                return new("Reader already has an active loan for this book.", ToneConstants.Amber);

            var dueAt = DateTime.Today.AddDays(GetLoanDays(reader.Level));
            var loan = new LoanRecord
            {
                Id = _nextLoanId++,
                BookId = book.Id,
                BookTitle = book.Title,
                ReaderId = reader.Id,
                ReaderName = reader.Name,
                BorrowedAt = DateTime.Today.ToString("yyyy-MM-dd"),
                DueAt = dueAt.ToString("yyyy-MM-dd"),
                Status = LoanStatus.Active
            };
            _loans.Add(loan);
            book.MonthlyBorrows++;
            book.LastActionAt = Clock.GetLocalNow().LocalDateTime;
            CompleteReservation(bookId, readerId);
            RecalculateBooks();
            RecalculateReaders();
            AddActivity($"{reader.Name} borrowed {book.Title}", ToneConstants.Blue);
            return new($"Borrowed {book.Title} to {reader.Name}, due {loan.DueAt}.", ToneConstants.Blue);
        }
    }

    public AdminNotice ReturnBook(int loanId)
    {
        lock (_sync)
        {
            var loan = FindOpenLoan(loanId);
            if (loan is null)
                return new("Active loan was not found.", ToneConstants.Red);

            var fine = CalculateFine(loan);
            loan.Status = LoanStatus.Returned;
            loan.ReturnedAt = DateTime.Today.ToString("yyyy-MM-dd");
            loan.Fine = fine;
            var reader = FindReader(loan.ReaderId);
            if (reader is not null)
                reader.FineBalance += fine;

            var book = FindBook(loan.BookId);
            if (book is not null)
                book.LastActionAt = Clock.GetLocalNow().LocalDateTime;

            RecalculateBooks();
            RecalculateReaders();
            AddActivity($"{loan.BookTitle} returned by {loan.ReaderName}", fine > 0 ? ToneConstants.Amber : ToneConstants.Green);
            return new(fine > 0 ? $"Returned with ${fine:0.00} fine." : $"Returned {loan.BookTitle}.", fine > 0 ? ToneConstants.Amber : ToneConstants.Green);
        }
    }

    public AdminNotice RenewLoan(int loanId)
    {
        lock (_sync)
        {
            var loan = FindOpenLoan(loanId);
            if (loan is null)
                return new("Active loan was not found.", ToneConstants.Red);
            if (loan.RenewCount >= 2)
                return new("This loan has reached the renewal limit.", ToneConstants.Amber);
            if (_reservations.Any(item => item.BookId == loan.BookId && item.Status == ReservationStatus.Waiting && item.ReaderId != loan.ReaderId))
                return new("This book has waiting reservations and cannot be renewed.", ToneConstants.Amber);

            var due = DateTime.Parse(loan.DueAt).AddDays(14);
            loan.DueAt = due.ToString("yyyy-MM-dd");
            loan.RenewCount++;
            loan.Status = due < DateTime.Today ? LoanStatus.Overdue : LoanStatus.Active;
            AddActivity($"Loan renewed: {loan.BookTitle} for {loan.ReaderName}", ToneConstants.Green);
            return new($"Renewed until {loan.DueAt}.", ToneConstants.Green);
        }
    }

    public AdminNotice ReserveBook(int bookId, int readerId)
    {
        lock (_sync)
        {
            var book = FindBook(bookId);
            var reader = FindReader(readerId);
            if (book is null)
                return new("Book was not found.", ToneConstants.Red);
            if (reader is null)
                return new("Reader was not found.", ToneConstants.Red);
            if (book.Status == BookStatus.Archived || reader.Status != ReaderStatus.Active)
                return new("Only active readers can reserve active books.", ToneConstants.Amber);
            if (_reservations.Any(item => item.BookId == bookId && item.ReaderId == readerId && item.Status == ReservationStatus.Waiting))
                return new("Reader already has a waiting reservation for this book.", ToneConstants.Amber);

            var reservation = new ReservationItem
            {
                Id = _nextReservationId++,
                BookId = book.Id,
                BookTitle = book.Title,
                ReaderId = reader.Id,
                ReaderName = reader.Name,
                CreatedAt = DateTime.Today.ToString("yyyy-MM-dd"),
                Status = ReservationStatus.Waiting
            };
            _reservations.Add(reservation);
            RecalculateBooks();
            RecalculateReaders();
            AddActivity($"{reader.Name} reserved {book.Title}", ToneConstants.Amber);
            return new($"Reserved {book.Title} for {reader.Name}.", ToneConstants.Amber);
        }
    }

    public AdminNotice CancelReservation(int reservationId)
    {
        lock (_sync)
        {
            var reservation = _reservations.FirstOrDefault(item => item.Id == reservationId && item.Status == ReservationStatus.Waiting);
            if (reservation is null)
                return new("Waiting reservation was not found.", ToneConstants.Red);

            reservation.Status = ReservationStatus.Cancelled;
            RecalculateBooks();
            RecalculateReaders();
            AddActivity($"Reservation cancelled: {reservation.BookTitle} for {reservation.ReaderName}", ToneConstants.Purple);
            return new("Reservation cancelled.", ToneConstants.Purple);
        }
    }

    public AdminNotice CollectFine(int readerId, decimal amount)
    {
        lock (_sync)
        {
            var reader = FindReader(readerId);
            if (reader is null)
                return new("Reader was not found.", ToneConstants.Red);

            var paid = amount <= 0 ? reader.FineBalance : Math.Min(reader.FineBalance, amount);
            reader.FineBalance -= paid;
            AddActivity($"Fine collected from {reader.Name}: ${paid:0.00}", ToneConstants.Green);
            return new($"Collected ${paid:0.00} from {reader.Name}.", ToneConstants.Green);
        }
    }

    public AdminNotice ReceiveCopies(int bookId, int quantity, string reason)
    {
        if (quantity <= 0)
            return new("Quantity must be greater than zero.", ToneConstants.Red);

        lock (_sync)
        {
            var book = FindBook(bookId);
            if (book is null)
                return new("Book was not found.", ToneConstants.Red);
            if (book.Status == BookStatus.Archived)
                return new("Restore the book before receiving new copies.", ToneConstants.Amber);

            book.CopyCount += quantity;
            book.LastActionAt = Clock.GetLocalNow().LocalDateTime;
            AddInventoryEvent(book, "Inbound", quantity, reason);
            RecalculateBooks();
            AddActivity($"Received {quantity} copies of {book.Title}", ToneConstants.Green);
            return new($"Received {quantity} copies of {book.Title}.", ToneConstants.Green);
        }
    }

    public AdminNotice WriteOffCopies(int bookId, int quantity, string reason)
    {
        if (quantity <= 0)
            return new("Quantity must be greater than zero.", ToneConstants.Red);

        lock (_sync)
        {
            var book = FindBook(bookId);
            if (book is null)
                return new("Book was not found.", ToneConstants.Red);

            var activeLoans = ActiveLoansFor(bookId).Count();
            var removableCopies = book.CopyCount - activeLoans;
            if (quantity > removableCopies)
                return new($"Only {removableCopies} copies can be written off because {activeLoans} are on loan.", ToneConstants.Amber);

            book.CopyCount -= quantity;
            book.LastActionAt = Clock.GetLocalNow().LocalDateTime;
            AddInventoryEvent(book, "WriteOff", -quantity, reason);
            RecalculateBooks();
            AddActivity($"Wrote off {quantity} copies of {book.Title}", ToneConstants.Purple);
            return new($"Wrote off {quantity} copies of {book.Title}.", ToneConstants.Purple);
        }
    }

    public AdminNotice RelocateBook(int bookId, string location, string reason)
    {
        if (string.IsNullOrWhiteSpace(location))
            return new("Location is required.", ToneConstants.Red);

        lock (_sync)
        {
            var book = FindBook(bookId);
            if (book is null)
                return new("Book was not found.", ToneConstants.Red);

            book.Location = location.Trim();
            book.LastActionAt = Clock.GetLocalNow().LocalDateTime;
            AddInventoryEvent(book, "Relocation", 0, reason);
            AddActivity($"Relocated {book.Title} to {book.Location}", ToneConstants.Blue);
            return new($"Relocated {book.Title} to {book.Location}.", ToneConstants.Blue);
        }
    }

    public string ExportBooksCsv(string query = "", string status = AllFilter, string category = AllFilter)
    {
        lock (_sync)
        {
            RecalculateBooks();

            var builder = new StringBuilder();
            builder.AppendLine("ISBN,Title,Author,Category,Location,PublishedYear,CopyCount,Rating");
            foreach (var book in FilterBooks(query, status, category))
            {
                builder.AppendLine(string.Join(',',
                    EscapeCsv(book.Isbn),
                    EscapeCsv(book.Title),
                    EscapeCsv(book.Author),
                    EscapeCsv(book.Category),
                    EscapeCsv(book.Location),
                    book.PublishedYear.ToString(CultureInfo.InvariantCulture),
                    book.CopyCount.ToString(CultureInfo.InvariantCulture),
                    book.Rating.ToString(CultureInfo.InvariantCulture)));
            }

            return builder.ToString();
        }
    }

    public AdminNotice ImportBooksCsv(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            return new("Paste CSV content before importing.", ToneConstants.Red);

        lock (_sync)
        {
            var rows = ParseCsv(csv).ToList();
            if (rows.Count == 0)
                return new("CSV content is empty.", ToneConstants.Red);

            var startIndex = LooksLikeHeader(rows[0]) ? 1 : 0;
            var added = 0;
            var updated = 0;
            var skipped = 0;

            for (var index = startIndex; index < rows.Count; index++)
            {
                var (result, bookAdded, bookUpdated) = ApplyImportedRow(rows[index], index);
                switch (result)
                {
                    case ImportResult.Added: added += bookAdded; break;
                    case ImportResult.Updated: updated += bookUpdated; break;
                    case ImportResult.Skipped: skipped++; break;
                }
            }

            RecalculateBooks();
            AddActivity($"CSV import completed: {added} added, {updated} updated, {skipped} skipped", skipped > 0 ? ToneConstants.Amber : ToneConstants.Green);
            return new($"CSV import completed: {added} added, {updated} updated, {skipped} skipped.", skipped > 0 ? ToneConstants.Amber : ToneConstants.Green);
        }
    }

    private enum ImportResult { Added, Updated, Skipped }

    private (ImportResult Result, int Added, int Updated) ApplyImportedRow(IReadOnlyList<string> row, int index)
    {
        if (row.Count == 0 || row.All(string.IsNullOrWhiteSpace))
            return (ImportResult.Skipped, 0, 0);

        var imported = ToBookImport(row);
        if (string.IsNullOrWhiteSpace(imported.Title) || string.IsNullOrWhiteSpace(imported.Author))
            return (ImportResult.Skipped, 0, 0);

        var book = string.IsNullOrWhiteSpace(imported.Isbn)
            ? null
            : _books.FirstOrDefault(item => item.Isbn.Equals(imported.Isbn, StringComparison.OrdinalIgnoreCase));

        if (book is null)
        {
            _books.Add(new BookItem
            {
                Id = _nextBookId++,
                Isbn = string.IsNullOrWhiteSpace(imported.Isbn) ? $"ISBN-{Clock.GetLocalNow().LocalDateTime:yyyyMMddHHmmss}-{index}" : imported.Isbn,
                Title = imported.Title,
                Author = imported.Author,
                Category = string.IsNullOrWhiteSpace(imported.Category) ? "General" : imported.Category,
                Location = string.IsNullOrWhiteSpace(imported.Location) ? "Import Shelf" : imported.Location,
                PublishedYear = imported.PublishedYear <= 0 ? DateTime.Today.Year : imported.PublishedYear,
                CopyCount = Math.Max(1, imported.CopyCount),
                AvailableCopies = Math.Max(1, imported.CopyCount),
                Rating = imported.Rating <= 0 ? 4.0m : imported.Rating,
                LastActionAt = Clock.GetLocalNow().LocalDateTime
            });
            return (ImportResult.Added, 1, 0);
        }

        var activeLoans = ActiveLoansFor(book.Id).Count();
        book.Title = imported.Title;
        book.Author = imported.Author;
        book.Category = string.IsNullOrWhiteSpace(imported.Category) ? book.Category : imported.Category;
        book.Location = string.IsNullOrWhiteSpace(imported.Location) ? book.Location : imported.Location;
        book.PublishedYear = imported.PublishedYear <= 0 ? book.PublishedYear : imported.PublishedYear;
        book.CopyCount = Math.Max(activeLoans, Math.Max(1, imported.CopyCount));
        book.Rating = imported.Rating <= 0 ? book.Rating : imported.Rating;
        book.LastActionAt = Clock.GetLocalNow().LocalDateTime;
        SyncLoanBookTitles(book);
        SyncReservationBookTitles(book);
        return (ImportResult.Updated, 0, 1);
    }

    private IEnumerable<BookItem> FilterBooks(string query, string status, string category)
    {
        var books = _books.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = query.Trim();
            books = books.Where(book => book.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || book.Author.Contains(term, StringComparison.OrdinalIgnoreCase)
                || book.Isbn.Contains(term, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrWhiteSpace(status) && status != AllFilter)
            books = books.Where(book => book.Status == status);
        if (!string.IsNullOrWhiteSpace(category) && category != AllFilter)
            books = books.Where(book => book.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        return books.OrderBy(book => book.Title);
    }

    private void Seed()
    {
        _readers.AddRange([
            new() { Id = _nextReaderId++, Name = "Alice", Email = "alice@example.com", Phone = "13800000001", Level = "Premium" },
            new() { Id = _nextReaderId++, Name = "Bob", Email = "bob@example.com", Phone = "13800000002", Level = "Standard" },
            new() { Id = _nextReaderId++, Name = "Chen", Email = "chen@example.com", Phone = "13800000003", Level = "Standard" },
            new() { Id = _nextReaderId++, Name = "Diana", Email = "diana@example.com", Phone = "13800000004", Level = "Student" }
        ]);

        AddSeedBook("9780134494166", "Clean Architecture", "Robert C. Martin", "Software", "A-01", 2017, 3, 42, 4.8m);
        AddSeedBook("9781449373320", "Designing Data-Intensive Applications", "Martin Kleppmann", "Software", "A-02", 2017, 2, 38, 4.9m);
        AddSeedBook("9780201616224", "The Pragmatic Programmer", "David Thomas", "Software", "A-03", 1999, 2, 35, 4.7m);
        AddSeedBook("9780735211292", "Atomic Habits", "James Clear", "Management", "B-11", 2018, 4, 29, 4.6m);
        AddSeedBook("9780374533557", "Thinking, Fast and Slow", "Daniel Kahneman", "Psychology", "C-08", 2011, 2, 31, 4.5m);
        AddSeedBook("9780385349369", "Creative Confidence", "Tom Kelley", "Innovation", "D-20", 2013, 1, 24, 4.4m);
        AddSeedBook("9781455586691", "Deep Work", "Cal Newport", "Management", "B-12", 2016, 3, 27, 4.6m);
        AddSeedBook("9780399590504", "Educated", "Tara Westover", "Biography", "E-04", 2018, 1, 19, 4.3m);

        BorrowBook(1, 1);
        BorrowBook(3, 2);
        BorrowBook(5, 3);
        BorrowBook(8, 4);
        ReserveBook(3, 1);
        _activities.Clear();
        _activities.AddRange([
            new("09:30", "Alice borrowed Clean Architecture", ToneConstants.Blue),
            new("10:20", "Designing Data-Intensive Applications returned to A-02", ToneConstants.Green),
            new("11:05", "Alice reserved The Pragmatic Programmer", ToneConstants.Amber),
            new("13:40", "Deep Work moved to featured shelf", ToneConstants.Purple)
        ]);
        RecalculateBooks();
        RecalculateReaders();
    }

    private void AddSeedBook(string isbn, string title, string author, string category, string location, int year, int copies, int borrows, decimal rating)
    {
        _books.Add(new BookItem
        {
            Id = _nextBookId++,
            Isbn = isbn,
            Title = title,
            Author = author,
            Category = category,
            Location = location,
            CopyCount = copies,
            AvailableCopies = copies,
            PublishedYear = year,
            MonthlyBorrows = borrows,
            Rating = rating,
            LastActionAt = DateTime.Today.AddDays(-1)
        });
    }

    private void RecalculateBooks()
    {
        foreach (var loan in _loans.Where(IsOpenLoan))
        {
            if (DateTime.Parse(loan.DueAt) < DateTime.Today)
                loan.Status = LoanStatus.Overdue;
        }

        foreach (var book in _books)
        {
            if (book.Status == BookStatus.Archived)
            {
                book.AvailableCopies = 0;
                book.StatusTone = ToneConstants.Purple;
                continue;
            }

            var activeLoans = ActiveLoansFor(book.Id).Count();
            book.AvailableCopies = Math.Max(0, book.CopyCount - activeLoans);
            var hasWaiting = _reservations.Any(item => item.BookId == book.Id && item.Status == ReservationStatus.Waiting);
            var currentLoan = _loans.LastOrDefault(item => item.BookId == book.Id && IsOpenLoan(item));
            book.Borrower = currentLoan?.ReaderName ?? string.Empty;
            book.DueDate = currentLoan?.DueAt ?? string.Empty;
            book.ReservedBy = _reservations.FirstOrDefault(item => item.BookId == book.Id && item.Status == ReservationStatus.Waiting)?.ReaderName ?? string.Empty;
            book.Status = currentLoan?.Status == LoanStatus.Overdue ? BookStatus.Overdue : book.AvailableCopies > 0 ? BookStatus.Available : hasWaiting ? BookStatus.Reserved : BookStatus.Borrowed;
            book.StatusTone = ToneAnalyzer.ForStatus(book.Status.ToString()) ?? ToneConstants.Blue;
        }
    }

    private void RecalculateReaders()
    {
        foreach (var reader in _readers)
        {
            reader.ActiveLoans = _loans.Count(loan => loan.ReaderId == reader.Id && IsOpenLoan(loan));
            reader.Reservations = _reservations.Count(item => item.ReaderId == reader.Id && item.Status == ReservationStatus.Waiting);
        }
    }

    private BookItem? FindBook(int bookId) => _books.FirstOrDefault(item => item.Id == bookId);

    private ReaderItem? FindReader(int readerId) => _readers.FirstOrDefault(item => item.Id == readerId);

    private LoanRecord? FindOpenLoan(int loanId) => _loans.FirstOrDefault(item => item.Id == loanId && IsOpenLoan(item));

    private IEnumerable<LoanRecord> ActiveLoansFor(int bookId) => _loans.Where(loan => loan.BookId == bookId && IsOpenLoan(loan));

    private static bool IsOpenLoan(LoanRecord loan) => loan.Status is LoanStatus.Active or LoanStatus.Overdue;

    private static bool IsWaitingReservation(ReservationItem reservation) => reservation.Status == ReservationStatus.Waiting;

    private static int GetLoanDays(string level) => level switch
    {
        "Premium" => 45,
        "Student" => 35,
        _ => 30
    };

    private static decimal CalculateFine(LoanRecord loan)
    {
        var overdueDays = (DateTime.Today - DateTime.Parse(loan.DueAt)).Days;
        return overdueDays > 0 ? overdueDays * 0.5m : 0m;
    }

    private void CompleteReservation(int bookId, int readerId)
    {
        var reservation = _reservations.FirstOrDefault(item => item.BookId == bookId && item.ReaderId == readerId && item.Status == ReservationStatus.Waiting);
        if (reservation is not null)
            reservation.Status = ReservationStatus.Completed;
    }

    private void CancelReservationsForBook(int bookId)
    {
        foreach (var item in _reservations.Where(item => item.BookId == bookId && item.Status == ReservationStatus.Waiting))
            item.Status = ReservationStatus.Cancelled;
    }

    private void CancelReservationsForReader(int readerId)
    {
        foreach (var item in _reservations.Where(item => item.ReaderId == readerId && item.Status == ReservationStatus.Waiting))
            item.Status = ReservationStatus.Cancelled;
    }

    private void SyncLoanBookTitles(BookItem book)
    {
        foreach (var loan in _loans.Where(item => item.BookId == book.Id))
            loan.BookTitle = book.Title;
    }

    private void SyncReservationBookTitles(BookItem book)
    {
        foreach (var item in _reservations.Where(item => item.BookId == book.Id))
            item.BookTitle = book.Title;
    }

    private void SyncLoanReaderNames(ReaderItem reader)
    {
        foreach (var loan in _loans.Where(item => item.ReaderId == reader.Id))
            loan.ReaderName = reader.Name;
    }

    private void SyncReservationReaderNames(ReaderItem reader)
    {
        foreach (var item in _reservations.Where(item => item.ReaderId == reader.Id))
            item.ReaderName = reader.Name;
    }

    private void AddActivity(string text, string tone)
    {
        _activities.Insert(0, new LibraryActivity(Clock.GetLocalNow().LocalDateTime.ToString("HH:mm"), text, tone));
    }

    private void AddInventoryEvent(BookItem book, string type, int quantity, string reason)
    {
        _inventoryEvents.Add(new InventoryEvent
        {
            Id = _nextInventoryEventId++,
            BookId = book.Id,
            BookTitle = book.Title,
            Type = type,
            Quantity = quantity,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Manual operation" : reason.Trim(),
            CreatedAt = Clock.GetLocalNow().LocalDateTime.ToString("yyyy-MM-dd HH:mm")
        });
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
            return value;

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }

    private static IEnumerable<List<string>> ParseCsv(string csv)
    {
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;

        for (var index = 0; index < csv.Length; index++)
        {
            var current = csv[index];
            if (current == '"')
            {
                if (inQuotes && index + 1 < csv.Length && csv[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
                continue;
            }

            if (current == ',' && !inQuotes)
            {
                row.Add(field.ToString().Trim());
                field.Clear();
                continue;
            }

            if ((current == '\n' || current == '\r') && !inQuotes)
            {
                if (current == '\r' && index + 1 < csv.Length && csv[index + 1] == '\n')
                    index++;

                row.Add(field.ToString().Trim());
                field.Clear();
                yield return row;
                row = [];
                continue;
            }

            field.Append(current);
        }

        row.Add(field.ToString().Trim());
        if (row.Count > 1 || !string.IsNullOrWhiteSpace(row[0]))
            yield return row;
    }

    private static bool LooksLikeHeader(IReadOnlyList<string> row)
    {
        return row.Count > 0 && row[0].Equals("ISBN", StringComparison.OrdinalIgnoreCase);
    }

    private static BookImport ToBookImport(IReadOnlyList<string> row)
    {
        return new BookImport(
            Get(row, 0),
            Get(row, 1),
            Get(row, 2),
            Get(row, 3),
            Get(row, 4),
            ParseInt(Get(row, 5)),
            ParseInt(Get(row, 6), 1),
            ParseDecimal(Get(row, 7), 4.0m));
    }

    private static string Get(IReadOnlyList<string> row, int index) => index < row.Count ? row[index].Trim() : string.Empty;

    private static int ParseInt(string value, int fallback = 0) => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : fallback;

    private static decimal ParseDecimal(string value, decimal fallback) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var result) ? result : fallback;

    private static BookItem Clone(BookItem book) => new()
    {
        Id = book.Id,
        Isbn = book.Isbn,
        Title = book.Title,
        Author = book.Author,
        Category = book.Category,
        Location = book.Location,
        Status = book.Status,
        StatusTone = book.StatusTone,
        CopyCount = book.CopyCount,
        AvailableCopies = book.AvailableCopies,
        Borrower = book.Borrower,
        ReservedBy = book.ReservedBy,
        DueDate = book.DueDate,
        PublishedYear = book.PublishedYear,
        MonthlyBorrows = book.MonthlyBorrows,
        Rating = book.Rating,
        LastActionAt = book.LastActionAt
    };

    private static ReaderItem Clone(ReaderItem reader) => new()
    {
        Id = reader.Id,
        Name = reader.Name,
        Email = reader.Email,
        Phone = reader.Phone,
        Level = reader.Level,
        Status = reader.Status,
        ActiveLoans = reader.ActiveLoans,
        Reservations = reader.Reservations,
        FineBalance = reader.FineBalance
    };

    private static LoanRecord Clone(LoanRecord loan) => new()
    {
        Id = loan.Id,
        BookId = loan.BookId,
        BookTitle = loan.BookTitle,
        ReaderId = loan.ReaderId,
        ReaderName = loan.ReaderName,
        BorrowedAt = loan.BorrowedAt,
        DueAt = loan.DueAt,
        ReturnedAt = loan.ReturnedAt,
        Status = loan.Status,
        RenewCount = loan.RenewCount,
        Fine = loan.Fine
    };

    private static ReservationItem Clone(ReservationItem item) => new()
    {
        Id = item.Id,
        BookId = item.BookId,
        BookTitle = item.BookTitle,
        ReaderId = item.ReaderId,
        ReaderName = item.ReaderName,
        CreatedAt = item.CreatedAt,
        Status = item.Status
    };

    private static InventoryEvent Clone(InventoryEvent item) => new()
    {
        Id = item.Id,
        BookId = item.BookId,
        BookTitle = item.BookTitle,
        Type = item.Type,
        Quantity = item.Quantity,
        Reason = item.Reason,
        CreatedAt = item.CreatedAt
    };
}

public record LibrarySnapshot(
    List<BookItem> Books,
    List<ReaderItem> Readers,
    List<LoanRecord> Loans,
    List<ReservationItem> Reservations,
    List<LibraryMetric> Metrics,
    List<CategoryStat> CategoryStats,
    List<BookItem> PopularBooks,
    List<InventoryEvent> InventoryEvents,
    List<LibraryActivity> Activities);
