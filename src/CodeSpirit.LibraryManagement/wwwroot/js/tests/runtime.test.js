var assert = { ok: function (condition, message) { if (!condition) throw new Error(message || 'Assertion failed'); }, equal: function (a, b, message) { if (a !== b) throw new Error((message || 'Assertion failed') + ' expected [' + b + '] got [' + a + ']'); }, notEqual: function (a, b, message) { if (a === b) throw new Error(message || 'Values should not be equal'); }, throws: function (fn, message) { try { fn(); throw new Error(message || 'Expected exception was not thrown'); } catch (e) { } } };

function run() {
  var passed = 0, failed = 0;
  function test(name, fn) {
    try { fn(); passed++; } catch (e) { failed++; console.error('FAIL: ' + name + ' - ' + e.message); }
  }

  test('CodeSpirit namespace defined', function () {
    assert.ok(typeof CodeSpirit !== 'undefined');
  });

  test('applyState sets text content', function () {
    var div = document.createElement('div');
    div.setAttribute('data-cs-bind', 'Name');
    document.body.appendChild(div);
    CodeSpirit.applyState(document.body, { Name: 'John' });
    assert.equal(div.textContent, 'John');
    document.body.removeChild(div);
  });

  test('applyState handles null value', function () {
    var div = document.createElement('div');
    div.setAttribute('data-cs-bind', 'Empty');
    document.body.appendChild(div);
    CodeSpirit.applyState(document.body, { Empty: null });
    assert.equal(div.textContent, '');
    document.body.removeChild(div);
  });

  test('applyState handles missing key gracefully', function () {
    var div = document.createElement('div');
    div.setAttribute('data-cs-bind', 'Missing');
    document.body.appendChild(div);
    CodeSpirit.applyState(document.body, {});
    assert.equal(div.textContent, '');
    document.body.removeChild(div);
  });

  test('applyState with CSS escape in name', function () {
    var div = document.createElement('div');
    div.setAttribute('data-cs-bind', 'Items[0].Name');
    document.body.appendChild(div);
    CodeSpirit.applyState(document.body, { 'Items[0].Name': 'Escaped' });
    assert.equal(div.textContent, 'Escaped');
    document.body.removeChild(div);
  });

  test('VmChain state returns initial values', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var input = document.createElement('input');
    input.setAttribute('name', 'email');
    input.value = 'test@test.com';
    form.appendChild(input);
    document.body.appendChild(form);

    var vm = CodeSpirit.vm(form);
    var state = vm.state();
    assert.equal(state.email, 'test@test.com');
    document.body.removeChild(form);
  });

  test('VmChain set updates input and state', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var input = document.createElement('input');
    input.setAttribute('name', 'title');
    form.appendChild(input);
    document.body.appendChild(form);

    var vm = CodeSpirit.vm(form);
    vm.set('title', 'New Title');
    assert.equal(input.value, 'New Title');
    assert.equal(vm.state().title, 'New Title');
    document.body.removeChild(form);
  });

  test('VmChain set with multiple fields', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var inputs = ['a', 'b', 'c'].map(function (name) {
      var inp = document.createElement('input');
      inp.setAttribute('name', name);
      form.appendChild(inp);
      return inp;
    });
    document.body.appendChild(form);

    var vm = CodeSpirit.vm(form);
    vm.set('a', '1');
    vm.set('b', '2');
    vm.set('c', '3');
    assert.equal(inputs[0].value, '1');
    assert.equal(inputs[1].value, '2');
    assert.equal(inputs[2].value, '3');
    document.body.removeChild(form);
  });

  test('VmChain observe fires on change', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var input = document.createElement('input');
    input.setAttribute('name', 'counter');
    form.appendChild(input);
    document.body.appendChild(form);

    var calls = [];
    var vm = CodeSpirit.vm(form);
    vm.observe('counter', function (v) { calls.push(v); });
    vm.set('counter', 'first');
    vm.set('counter', 'second');
    assert.equal(calls.length, 2);
    assert.equal(calls[0], 'first');
    assert.equal(calls[1], 'second');
    document.body.removeChild(form);
  });

  test('VmChain observe notifies same value set', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var input = document.createElement('input');
    input.setAttribute('name', 'stable');
    input.value = 'x';
    form.appendChild(input);
    document.body.appendChild(form);

    var calls = [];
    var vm = CodeSpirit.vm(form);
    vm.observe('stable', function (v) { calls.push(v); });
    vm.set('stable', 'x');
    assert.equal(calls.length, 1);
    assert.equal(calls[0], 'x');
    document.body.removeChild(form);
  });

  test('VmChain validate required field', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var input = document.createElement('input');
    input.setAttribute('name', 'requiredField');
    form.appendChild(input);
    document.body.appendChild(form);

    var vm = CodeSpirit.vm(form);
    assert.ok(vm.validate({ requiredField: { required: true } }) === false);
    input.value = 'ready';
    assert.ok(vm.validate({ requiredField: { required: true } }) === true);
    document.body.removeChild(form);
  });

  test('VmChain validate min length', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var input = document.createElement('input');
    input.setAttribute('name', 'code');
    input.value = 'abc';
    form.appendChild(input);
    document.body.appendChild(form);

    var vm = CodeSpirit.vm(form);
    assert.ok(vm.validate({ code: { minLength: 3 } }) === true);
    assert.ok(vm.validate({ code: { minLength: 5 } }) === false);
    document.body.removeChild(form);
  });

  test('VmChain checkbox group returns checked values', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var a = document.createElement('input');
    a.type = 'checkbox';
    a.setAttribute('name', 'tags');
    a.value = 'a';
    a.checked = true;
    var b = document.createElement('input');
    b.type = 'checkbox';
    b.setAttribute('name', 'tags');
    b.value = 'b';
    form.appendChild(a);
    form.appendChild(b);
    document.body.appendChild(form);

    var value = CodeSpirit.vm(form).get('tags');
    assert.equal(value.length, 1);
    assert.equal(value[0], 'a');
    document.body.removeChild(form);
  });

  test('VmChain set updates checkbox and radio controls', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var check = document.createElement('input');
    check.type = 'checkbox';
    check.setAttribute('name', 'enabled');
    check.value = 'yes';
    var radio = document.createElement('input');
    radio.type = 'radio';
    radio.setAttribute('name', 'mode');
    radio.value = 'fast';
    form.appendChild(check);
    form.appendChild(radio);
    document.body.appendChild(form);

    var vm = CodeSpirit.vm(form);
    vm.set('enabled', 'yes').set('mode', 'fast');
    assert.equal(check.checked, true);
    assert.equal(radio.checked, true);
    document.body.removeChild(form);
  });

  test('checkbox input emits checked value', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var check = document.createElement('input');
    check.type = 'checkbox';
    check.setAttribute('name', 'enabled');
    check.value = 'yes';
    var mirror = document.createElement('span');
    mirror.setAttribute('data-cs-bind', 'enabled');
    form.appendChild(check);
    form.appendChild(mirror);
    document.body.appendChild(form);

    check.checked = true;
    check.dispatchEvent(new Event('input', { bubbles: true }));
    assert.equal(mirror.textContent, 'yes');
    check.checked = false;
    check.dispatchEvent(new Event('input', { bubbles: true }));
    assert.equal(mirror.textContent, '');
    document.body.removeChild(form);
  });

  test('VmChain batch set notifies observers once per field', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var a = document.createElement('input');
    a.setAttribute('name', 'a');
    var b = document.createElement('input');
    b.setAttribute('name', 'b');
    form.appendChild(a);
    form.appendChild(b);
    document.body.appendChild(form);

    var calls = [];
    var vm = CodeSpirit.vm(form);
    vm.observe(['a', 'b'], function (value, field) { calls.push(field + ':' + value); });
    vm.set({ a: '1', b: '2' });
    assert.equal(calls.length, 2);
    assert.equal(calls[0], 'a:1');
    assert.equal(calls[1], 'b:2');
    document.body.removeChild(form);
  });

  test('VmChain validate email and numeric range', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var email = document.createElement('input');
    email.setAttribute('name', 'email');
    email.value = 'bad';
    var age = document.createElement('input');
    age.setAttribute('name', 'age');
    age.value = '17';
    form.appendChild(email);
    form.appendChild(age);
    document.body.appendChild(form);

    var vm = CodeSpirit.vm(form);
    assert.ok(vm.validate({ email: { email: true }, age: { min: 18, max: 65 } }) === false);
    email.value = 'ada@example.com';
    age.value = '21';
    assert.ok(vm.validate({ email: { email: true }, age: { min: 18, max: 65 } }) === true);
    document.body.removeChild(form);
  });

  test('VmChain validate url, numeric, integer and phone', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var website = document.createElement('input');
    website.setAttribute('name', 'website');
    website.value = 'not-a-url';
    var score = document.createElement('input');
    score.setAttribute('name', 'score');
    score.value = 'abc';
    var count = document.createElement('input');
    count.setAttribute('name', 'count');
    count.value = '12.5';
    var phone = document.createElement('input');
    phone.setAttribute('name', 'phone');
    phone.value = 'invalid#phone';
    form.appendChild(website);
    form.appendChild(score);
    form.appendChild(count);
    form.appendChild(phone);
    document.body.appendChild(form);

    var vm = CodeSpirit.vm(form);
    assert.ok(vm.validate({ website: { url: true } }) === false);
    assert.ok(vm.validate({ score: { numeric: true } }) === false);
    assert.ok(vm.validate({ count: { integer: true } }) === false);
    assert.ok(vm.validate({ phone: { phone: true } }) === false);

    website.value = 'https://codespirit.dev';
    score.value = '-42.8';
    count.value = '100';
    phone.value = '+1-555-0199';
    assert.ok(vm.validate({
      website: { url: true },
      score: { numeric: true },
      count: { integer: true },
      phone: { phone: true }
    }) === true);
    document.body.removeChild(form);
  });

  test('VmChain validate matches cross-field rule', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var pwd = document.createElement('input');
    pwd.setAttribute('name', 'password');
    pwd.value = 'secret123';
    var confirm = document.createElement('input');
    confirm.setAttribute('name', 'confirmPassword');
    confirm.value = 'secret456';
    form.appendChild(pwd);
    form.appendChild(confirm);
    document.body.appendChild(form);

    var vm = CodeSpirit.vm(form);
    assert.ok(vm.validate({ confirmPassword: { matches: 'password' } }) === false);
    confirm.value = 'secret123';
    assert.ok(vm.validate({ confirmPassword: { matches: 'password' } }) === true);
    document.body.removeChild(form);
  });

  test('CodeSpirit.registerValidator handles custom global rule', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var code = document.createElement('input');
    code.setAttribute('name', 'promoCode');
    code.value = 'NOPE';
    form.appendChild(code);
    document.body.appendChild(form);

    CodeSpirit.registerValidator('startsCode', function (val, param) {
      return String(val || '').startsWith(param) || 'Must start with ' + param;
    });

    var vm = CodeSpirit.vm(form);
    assert.ok(vm.validate({ promoCode: { startsCode: 'CSP' } }) === false);
    code.value = 'CSP-2026';
    assert.ok(vm.validate({ promoCode: { startsCode: 'CSP' } }) === true);
    document.body.removeChild(form);
  });

  test('data-cs-attr blocks unsafe dynamic attributes', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var input = document.createElement('input');
    input.setAttribute('name', 'Url');
    var link = document.createElement('a');
    link.setAttribute('data-cs-attr', 'Url:href');
    form.appendChild(input);
    form.appendChild(link);
    document.body.appendChild(form);

    var vm = CodeSpirit.vm(form);
    vm.set('Url', 'javascript:alert(1)');
    assert.equal(link.getAttribute('href'), null);
    vm.set('Url', '/safe');
    assert.equal(link.getAttribute('href'), '/safe');
    document.body.removeChild(form);
  });

  test('Expression isExpr detects binding syntax', function () {
    assert.ok(CodeSpirit.expression.isExpr('Name == "John"'));
    assert.ok(CodeSpirit.expression.isExpr('contains(Name, "Jo")'));
    assert.ok(CodeSpirit.expression.isExpr('length(Name)'));
    assert.ok(CodeSpirit.expression.isExpr('coalesce(Name, Alternative)'));
    assert.ok(!CodeSpirit.expression.isExpr('plain text'));
    assert.ok(!CodeSpirit.expression.isExpr(''));
    assert.ok(!CodeSpirit.expression.isExpr(null));
  });

  test('Expression evaluate simple comparison', function () {
    var root = document.body;
    assert.equal(CodeSpirit.expression.evaluate('5 > 3', root), true);
    assert.equal(CodeSpirit.expression.evaluate('5 < 3', root), false);
    assert.equal(CodeSpirit.expression.evaluate('5 == 5', root), true);
    assert.equal(CodeSpirit.expression.evaluate('5 != 5', root), false);
  });

  test('Expression evaluate logical operators', function () {
    var root = document.body;
    assert.equal(CodeSpirit.expression.evaluate('true && true', root), true);
    assert.equal(CodeSpirit.expression.evaluate('true && false', root), false);
    assert.equal(CodeSpirit.expression.evaluate('false || true', root), true);
    assert.equal(CodeSpirit.expression.evaluate('false || false', root), false);
  });

  test('Expression evaluate contains function', function () {
    var root = document.body;
    assert.equal(CodeSpirit.expression.evaluate("contains('hello world', 'world')", root), true);
    assert.equal(CodeSpirit.expression.evaluate("contains('hello world', 'xyz')", root), false);
    assert.equal(CodeSpirit.expression.evaluate("contains('John\\'s book', '\\'s')", root), true);
  });

  test('Expression evaluate empty function', function () {
    var root = document.body;
    assert.equal(CodeSpirit.expression.evaluate("empty('')", root), true);
    assert.equal(CodeSpirit.expression.evaluate("empty('nonempty')", root), false);
    assert.equal(CodeSpirit.expression.evaluate('empty(null)', root), true);
  });

  test('Expression evaluate length function', function () {
    var root = document.body;
    assert.equal(CodeSpirit.expression.evaluate("length('abc')", root), 3);
    assert.equal(CodeSpirit.expression.evaluate("length('')", root), 0);
  });

  test('Expression evaluate trim function', function () {
    var root = document.body;
    assert.equal(CodeSpirit.expression.evaluate("trim('  hello  ')", root), 'hello');
  });

  test('Expression evaluate lower/upper functions', function () {
    var root = document.body;
    assert.equal(CodeSpirit.expression.evaluate("lower('HELLO')", root), 'hello');
    assert.equal(CodeSpirit.expression.evaluate("upper('hello')", root), 'HELLO');
  });

  test('Expression evaluate startsWith/endsWith', function () {
    var root = document.body;
    assert.equal(CodeSpirit.expression.evaluate("startsWith('hello world', 'hello')", root), true);
    assert.equal(CodeSpirit.expression.evaluate("startsWith('hello world', 'world')", root), false);
    assert.equal(CodeSpirit.expression.evaluate("endsWith('hello world', 'world')", root), true);
    assert.equal(CodeSpirit.expression.evaluate("endsWith('hello world', 'hello')", root), false);
  });

  test('Expression evaluate substring function', function () {
    var root = document.body;
    assert.equal(CodeSpirit.expression.evaluate("substring('hello', 1, 3)", root), 'el');
  });

  test('Expression evaluate math functions', function () {
    var root = document.body;
    assert.equal(CodeSpirit.expression.evaluate('abs(-5)', root), 5);
    assert.equal(CodeSpirit.expression.evaluate('min(3, 7, 1)', root), 1);
    assert.equal(CodeSpirit.expression.evaluate('max(3, 7, 1)', root), 7);
    assert.equal(CodeSpirit.expression.evaluate('round(3.7)', root), 4);
    assert.equal(CodeSpirit.expression.evaluate('floor(3.7)', root), 3);
    assert.equal(CodeSpirit.expression.evaluate('ceil(3.2)', root), 4);
  });

  test('Expression evaluate coalesce function', function () {
    var root = document.body;
    assert.equal(CodeSpirit.expression.evaluate("coalesce(null, 'default')", root), 'default');
    assert.equal(CodeSpirit.expression.evaluate("coalesce('value', 'default')", root), 'value');
  });

  test('Expression evaluate isNull function', function () {
    var root = document.body;
    assert.equal(CodeSpirit.expression.evaluate('isNull(null)', root), true);
    assert.equal(CodeSpirit.expression.evaluate("isNull('notnull')", root), false);
  });

  test('Expression evaluate ternary expressions', function () {
    var root = document.body;
    assert.equal(CodeSpirit.expression.evaluate("true ? 'yes' : 'no'", root), 'yes');
    assert.equal(CodeSpirit.expression.evaluate("false ? 'yes' : 'no'", root), 'no');
  });

  test('Expression evaluate complex nested', function () {
    var root = document.body;
    assert.equal(CodeSpirit.expression.evaluate('(5 > 3) && (2 < 4)', root), true);
    assert.equal(CodeSpirit.expression.evaluate('(5 < 3) || (2 < 4)', root), true);
    assert.equal(CodeSpirit.expression.evaluate('!(true && false)', root), true);
  });

  test('Expression parse returns AST', function () {
    var ast = CodeSpirit.expression.parse('a > 5');
    assert.ok(ast !== null);
    assert.ok(typeof ast === 'object');
  });

  test('Expression parse empty input returns null', function () {
    assert.equal(CodeSpirit.expression.parse(''), null);
    assert.equal(CodeSpirit.expression.parse(null), null);
  });

  test('Expression apply evaluates against context', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var panel = document.createElement('div');
    panel.setAttribute('data-cs-show', '5 > 3');
    form.appendChild(panel);
    document.body.appendChild(form);

    CodeSpirit.expression.apply(form);
    assert.equal(panel.style.display, '');
    document.body.removeChild(form);
  });

  test('Expression field reference in form context', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var input = document.createElement('input');
    input.setAttribute('name', 'age');
    input.value = '25';
    form.appendChild(input);
    document.body.appendChild(form);

    var result = CodeSpirit.expression.evaluate('age > 18', form);
    assert.equal(result, true);
    document.body.removeChild(form);
  });

  test('Expression number field comparison', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var input = document.createElement('input');
    input.setAttribute('name', 'count');
    input.value = '10';
    form.appendChild(input);
    document.body.appendChild(form);

    assert.equal(CodeSpirit.expression.evaluate('count >= 10', form), true);
    assert.equal(CodeSpirit.expression.evaluate('count < 5', form), false);
    document.body.removeChild(form);
  });

  test('Intent analyze returns default for empty input', function () {
    var root = document.createElement('main');
    document.body.appendChild(root);
    CodeSpirit.intent.analyze(root);
    assert.ok(root.getAttribute('data-cs-scene-score') !== null);
    document.body.removeChild(root);
  });

  test('Intent analyze detects dashboard scene', function () {
    var root = document.createElement('main');
    var title = document.createElement('h1');
    title.textContent = 'dashboard metrics chart';
    root.appendChild(title);
    document.body.appendChild(root);
    CodeSpirit.intent.analyze(root);
    assert.ok(root.classList.contains('cs-scene-dashboard'));
    document.body.removeChild(root);
  });

  test('Intent analyze detects library scene', function () {
    var root = document.createElement('main');
    var title = document.createElement('h1');
    title.textContent = 'library book management';
    root.appendChild(title);
    document.body.appendChild(root);
    CodeSpirit.intent.analyze(root);
    assert.ok(root.classList.contains('cs-scene-library'));
    document.body.removeChild(root);
  });

  test('Intent analyze returns theme tokens', function () {
    var root = document.createElement('main');
    root.setAttribute('data-cs-scene', 'admin');
    document.body.appendChild(root);
    CodeSpirit.intent.analyze(root);
    assert.ok(root.classList.contains('cs-scene-admin'));
    document.body.removeChild(root);
  });

  test('Intent register adds custom analyzer', function () {
    var called = false;
    CodeSpirit.intent.register('test', function () {
      called = true;
      return { scene: 'test' };
    });
    assert.ok(typeof CodeSpirit.intent.analyze === 'function');
  });

  test('Theme tokens returns default when no scene', function () {
    if (typeof CodeSpirit.theme !== 'undefined') {
      var tokens = CodeSpirit.theme.tokens();
      assert.ok(typeof tokens === 'object');
      assert.ok(tokens['--cs-primary'] !== undefined);
    }
  });

  test('Theme tokens returns scene-specific tokens', function () {
    if (typeof CodeSpirit.theme !== 'undefined') {
      var tokens = CodeSpirit.theme.tokens('dashboard');
      assert.ok(typeof tokens === 'object');
    }
  });

  test('Theme exportTokens returns CSS string', function () {
    if (typeof CodeSpirit.theme !== 'undefined') {
      var tokens = CodeSpirit.theme.exportTokens();
      assert.ok(typeof tokens === 'object');
      assert.ok(tokens['--cs-primary'] !== undefined);
    }
  });

  test('input sets form field value', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var input = document.createElement('input');
    input.setAttribute('name', 'field');
    form.appendChild(input);
    document.body.appendChild(form);

    CodeSpirit.input(form, 'field', 'testValue');
    assert.equal(input.value, 'testValue');
    document.body.removeChild(form);
  });

  test('input with null sets empty string', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var input = document.createElement('input');
    input.setAttribute('name', 'field');
    input.value = 'old';
    form.appendChild(input);
    document.body.appendChild(form);

    CodeSpirit.input(form, 'field', null);
    assert.equal(input.value, '');
    document.body.removeChild(form);
  });

  test('qs and qsa query scoped elements', function () {
    var root = document.createElement('div');
    var one = document.createElement('span');
    one.setAttribute('class', 'item');
    var two = document.createElement('span');
    two.setAttribute('class', 'item');
    root.appendChild(one);
    root.appendChild(two);
    document.body.appendChild(root);

    assert.equal(CodeSpirit.qs('.item', root), one);
    assert.equal(CodeSpirit.qsa('.item', root).length, 2);
    document.body.removeChild(root);
  });

  test('on returns unsubscribe function for delegated events', function () {
    var root = document.createElement('div');
    var button = document.createElement('button');
    button.setAttribute('class', 'action');
    root.appendChild(button);
    document.body.appendChild(root);

    var calls = 0;
    var off = CodeSpirit.on(root, 'click', '.action', function () { calls++; });
    button.dispatchEvent(new Event('click', { bubbles: true }));
    off();
    button.dispatchEvent(new Event('click', { bubbles: true }));
    assert.equal(calls, 1);
    document.body.removeChild(root);
  });

  test('batch updates state and returns VmChain', function () {
    var form = document.createElement('form');
    form.setAttribute('data-cs-vm', '');
    var input = document.createElement('input');
    input.setAttribute('name', 'Name');
    form.appendChild(input);
    document.body.appendChild(form);

    var chain = CodeSpirit.batch(form, { Name: 'Ada' });
    assert.ok(chain instanceof CodeSpirit.VmChain);
    assert.equal(input.value, 'Ada');
    assert.equal(CodeSpirit.state(form).Name, 'Ada');
    document.body.removeChild(form);
  });

  test('debounce delays execution', function () {
    var calls = 0;
    var debounced = CodeSpirit.debounce(function () { calls++; }, 0);
    debounced();
    debounced();
    assert.equal(calls, 0);
  });

  test('mount returns target element', function () {
    var container = document.createElement('div');
    document.body.appendChild(container);

    assert.equal(CodeSpirit.mount(container), container);
    document.body.removeChild(container);
  });

  test('mount defaults to document', function () {
    var container = document.createElement('div');
    document.body.appendChild(container);

    assert.equal(CodeSpirit.mount(), document);
    document.body.removeChild(container);
  });

  test('refresh returns mounted target', function () {
    var container = document.createElement('div');
    document.body.appendChild(container);

    assert.equal(CodeSpirit.refresh(container), container);
    document.body.removeChild(container);
  });

  test('rendering templating handles missing bindings gracefully', function () {
    var div = document.createElement('div');
    div.setAttribute('data-cs-bind', 'NonExistent');
    document.body.appendChild(div);
    CodeSpirit.applyState(document.body, {});
    assert.equal(div.textContent, '');
    document.body.removeChild(div);
  });

  test('applyState handles boolean values', function () {
    var div = document.createElement('div');
    div.setAttribute('data-cs-bind', 'IsActive');
    document.body.appendChild(div);
    CodeSpirit.applyState(document.body, { IsActive: true });
    assert.equal(div.textContent, 'true');
    CodeSpirit.applyState(document.body, { IsActive: false });
    assert.equal(div.textContent, 'false');
    document.body.removeChild(div);
  });

  test('applyState handles numeric values', function () {
    var div = document.createElement('div');
    div.setAttribute('data-cs-bind', 'Count');
    document.body.appendChild(div);
    CodeSpirit.applyState(document.body, { Count: 42 });
    assert.equal(div.textContent, '42');
    CodeSpirit.applyState(document.body, { Count: 0 });
    assert.equal(div.textContent, '0');
    document.body.removeChild(div);
  });

  console.log('PASSED: ' + passed + '/' + (passed + failed) + ' tests');

  if (failed > 0) {
    var failEl = document.querySelector('.js-test-failed');
    if (failEl) failEl.style.display = 'block';
  } else {
    var passEl = document.querySelector('.js-test-passed');
    if (passEl) passEl.style.display = 'block';
  }
}

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', run);
} else {
  run();
}
