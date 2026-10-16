; Unshipped analyzer releases

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
CSP001 | CodeSpirit | Warning | Abstract class with [Service] is ignored
CSP002 | CodeSpirit | Warning | [Service] class missing public constructor
CSP003 | CodeSpirit | Error | [Command] method must not have parameters
CSP004 | CodeSpirit | Warning | [Transactional] or [Cacheable] on non-virtual method has no effect
CSP005 | CodeSpirit | Warning | [Autowired] or [Value] on non-writable property has no effect
CSP006 | CodeSpirit | Warning | [Bind] on non-public property
CSP007 | CodeSpirit | Warning | [Command] on non-public or static method is unreachable
CSP008 | CodeSpirit | Warning | [Transactional] or [Cacheable] on non-[Service] class has no effect
CSP009 | CodeSpirit | Warning | Duplicate command name in ViewModel
CSP010 | CodeSpirit | Warning | Duplicate binding name in ViewModel
CSP011 | CodeSpirit | Warning | Command name uses reserved prefix
CSP012 | CodeSpirit | Warning | Async void command method
CSP013 | CodeSpirit | Info | ViewModel missing [PageDirective]
CSP014 | CodeSpirit | Warning | Invalid command return type
CSP015 | CodeSpirit | Warning | [PageDirective] class does not inherit ViewModel
