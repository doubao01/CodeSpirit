const fs = require('fs');
const vm = require('vm');
const assert = require('assert');
const path = require('path');

const templateRoot = path.resolve(__dirname, '..');

class Event {
  constructor(type, options = {}) {
    this.type = type;
    this.bubbles = Boolean(options.bubbles);
    this.detail = options.detail;
    this.key = options.key;
    this.defaultPrevented = false;
    this.target = null;
    this.submitter = options.submitter || null;
  }

  preventDefault() {
    this.defaultPrevented = true;
  }

  stopImmediatePropagation() {}
}

class EventTarget {
  constructor() {
    this.listeners = {};
    this.parentNode = null;
  }

  addEventListener(type, handler) {
    this.listeners[type] = this.listeners[type] || [];
    this.listeners[type].push(handler);
  }

  removeEventListener(type, handler) {
    if (!this.listeners[type]) return;
    const idx = this.listeners[type].indexOf(handler);
    if (idx >= 0) this.listeners[type].splice(idx, 1);
  }

  dispatchEvent(event) {
    event.target = event.target || this;
    (this.listeners[event.type] || []).forEach((handler) => handler.call(this, event));
    if (event.bubbles && this.parentNode) {
      this.parentNode.dispatchEvent(event);
    }
    return !event.defaultPrevented;
  }
}

function matchesSelector(element, selector) {
  var remaining = selector.trim();

  if (remaining.charAt(0) === '.') {
    var dotIdx = remaining.search(/[\[\s:]|$/);
    var className = remaining.substring(1, dotIdx);
    return element.classList.contains(className);
  }

  if (remaining.charAt(0) === '#') {
    var hashIdx = remaining.search(/[\[\s:]|$/);
    var id = remaining.substring(1, hashIdx);
    return element.getAttribute('id') === id;
  }

  var tagName = null;
  var tagMatch = remaining.match(/^([A-Za-z][A-Za-z0-9_-]*)/);
  if (tagMatch) {
    tagName = tagMatch[1].toLowerCase();
    remaining = remaining.substring(tagMatch[0].length).trim();
  }

  if (tagName && element.tagName.toLowerCase() !== tagName) {
    return false;
  }

  while (remaining.length > 0) {
    var attrEquals = remaining.match(/^\[([^=~\]]+)="([^"]*)"\]/);
    if (attrEquals) {
      if (element.getAttribute(attrEquals[1]) !== attrEquals[2]) return false;
      remaining = remaining.substring(attrEquals[0].length).trim();
      continue;
    }

    var attrToken = remaining.match(/^\[([^=~\]]+)~="([^"]*)"\]/);
    if (attrToken) {
      if (!(element.getAttribute(attrToken[1]) || '').split(/\s+/).includes(attrToken[2])) return false;
      remaining = remaining.substring(attrToken[0].length).trim();
      continue;
    }

    var attrExists = remaining.match(/^\[([^\]]+)\]/);
    if (attrExists) {
      if (element.getAttribute(attrExists[1]) == null) return false;
      remaining = remaining.substring(attrExists[0].length).trim();
      continue;
    }

    break;
  }

  if (remaining.length === 0) {
    return true;
  }

  return element.tagName.toLowerCase() === selector.toLowerCase();
}

class Element extends EventTarget {
  constructor(tagName, attributes = {}) {
    super();
    this.nodeType = 1;
    this.tagName = tagName.toUpperCase();
    this.attributes = { ...attributes };
    this.children = [];
    this.style = {};
    this._textContent = '';
    this.textContent = '';
    this.name = attributes.name || '';
    this.classList = new ClassListMock();
    String(attributes.class || '').split(/\s+/).filter(Boolean).forEach((className) => this.classList.add(className));
    if (['INPUT', 'BUTTON', 'SELECT', 'TEXTAREA'].includes(this.tagName)) {
      this.value = attributes.value || '';
    }
    this.disabled = Object.prototype.hasOwnProperty.call(attributes, 'disabled');
    this.closest_cache = null;
  }

  appendChild(child) {
    child.parentNode = this;
    this.children.push(child);
    return child;
  }

  removeChild(child) {
    const index = this.children.indexOf(child);
    if (index >= 0) {
      this.children.splice(index, 1);
      child.parentNode = null;
    }
    return child;
  }

  createElement(tagName) {
    return new Element(tagName);
  }

  get firstElementChild() {
    return this.children[0] || null;
  }

  get textContent() {
    return this._textContent;
  }

  set textContent(value) {
    this._textContent = value == null ? '' : String(value);
  }

  set innerHTML(value) {
    this.children = [];
    const source = String(value).trim();
    const elementRegex = /<([A-Za-z][A-Za-z0-9-]*)([^>]*)>([\s\S]*?)<\/\1>/g;
    let matched = false;
    let elementMatch;

    while ((elementMatch = elementRegex.exec(source)) !== null) {
      matched = true;
      const [, tagName, rawAttributes, content] = elementMatch;
      const attributes = {};
      rawAttributes.replace(/([A-Za-z][A-Za-z0-9_-]*)="([^"]*)"/g, (_, name, attrValue) => {
        attributes[name] = attrValue;
        return '';
      });

      const element = this.appendChild(new Element(tagName, attributes));
      element.textContent = content.replace(/<[^>]+>/g, '');
      if (/^\s*<[A-Za-z][A-Za-z0-9-]*[\s>]/.test(content)) {
        element.innerHTML = content.trim();
      }
    }

    if (matched) {
      return;
    }
  }

  getAttribute(name) {
    return Object.prototype.hasOwnProperty.call(this.attributes, name) ? this.attributes[name] : null;
  }

  setAttribute(name, value) {
    this.attributes[name] = String(value);
    if (name === 'class') {
      this.classList = new ClassListMock();
      String(value).split(/\s+/).filter(Boolean).forEach((className) => this.classList.add(className));
    }
    if (name === 'name') {
      this.name = String(value);
    }
    if (name === 'disabled') {
      this.disabled = true;
    }
  }

  removeAttribute(name) {
    delete this.attributes[name];
    if (name === 'name') {
      this.name = '';
    }
    if (name === 'disabled') {
      this.disabled = false;
    }
  }

  hasAttribute(name) {
    return Object.prototype.hasOwnProperty.call(this.attributes, name);
  }

  contains(node) {
    let current = node;
    while (current) {
      if (current === this) {
        return true;
      }
      current = current.parentNode;
    }
    return false;
  }

  requestSubmit() {
    var event = new Event('submit', { bubbles: true });
    event.submitter = null;
    this.dispatchEvent(event);
  }

  matches(selector) {
    return matchesSelector(this, selector);
  }

  closest(selector) {
    let current = this;
    while (current) {
      if (current.matches && current.matches(selector)) {
        return current;
      }
      current = current.parentNode;
    }
    return null;
  }

  querySelectorAll(selector) {
    const selectors = selector.split(',').map((item) => item.trim());
    const results = [];

    function visit(node) {
      node.children.forEach((child) => {
        if (selectors.some((item) => child.matches(item))) {
          results.push(child);
        }
        visit(child);
      });
    }

    visit(this);
    return results;
  }

  querySelector(selector) {
    return this.querySelectorAll(selector)[0] || null;
  }

  replaceWith(next) {
    if (!this.parentNode) {
      return;
    }

    const index = this.parentNode.children.indexOf(this);
    if (index >= 0) {
      next.parentNode = this.parentNode;
      this.parentNode.children[index] = next;
      this.parentNode = null;
    }
  }

  replaceChildren(...nodes) {
    this.children.forEach((child) => { child.parentNode = null; });
    this.children = [];
    nodes.forEach((node) => this.appendChild(node));
  }
}

class ClassListMock {
  constructor() {
    this._classes = [];
  }

  get length() {
    return this._classes.length;
  }

  _syncIndexes() {
    Object.keys(this).forEach((key) => {
      if (/^\d+$/.test(key)) delete this[key];
    });
    this._classes.forEach((className, index) => {
      this[index] = className;
    });
  }

  add(className) {
    if (!this._classes.includes(className)) {
      this._classes.push(className);
      this._syncIndexes();
    }
  }

  remove(className) {
    const idx = this._classes.indexOf(className);
    if (idx >= 0) {
      this._classes.splice(idx, 1);
      this._syncIndexes();
    }
  }

  contains(className) {
    return this._classes.includes(className);
  }

  toggle(className, force) {
    if (force === true) {
      this.add(className);
      return true;
    }
    if (force === false) {
      this.remove(className);
      return false;
    }

    if (this.contains(className)) {
      this.remove(className);
      return false;
    } else {
      this.add(className);
      return true;
    }
  }

  toString() {
    return this._classes.join(' ');
  }

  [Symbol.iterator]() {
    return this._classes[Symbol.iterator]();
  }
}

class Document extends Element {
  constructor() {
    super('#document');
    this.nodeType = 9;
    this.title = '';
    this.documentElement = this.appendChild(new Element('html'));
    this.body = this.appendChild(new Element('body'));
  }

  matches() {
    return false;
  }

  createElement(tagName) {
    return new Element(tagName);
  }
}

class FormDataStub {
  constructor(form) {
    this.fields = [];
    form.querySelectorAll('[name]').forEach((element) => {
      this.fields.push([element.name, element.value]);
    });
  }

  forEach(callback) {
    this.fields.forEach(([key, value]) => callback(value, key));
  }
}

function runScript(file, context) {
  vm.runInContext(fs.readFileSync(path.join(templateRoot, file), 'utf8'), context, { filename: file });
}

function validateSnippetCatalog() {
  const workspaceRoot = path.resolve(templateRoot, '../..');
  const snippetFile = path.join(workspaceRoot, '.vscode/codespirit.code-snippets');
  const snippets = JSON.parse(fs.readFileSync(snippetFile, 'utf8'));
  const prefixes = new Set(Object.values(snippets).map((snippet) => snippet.prefix));
  const requiredPrefixes = [
    'cs-show', 'cs-enable', 'cs-refresh', 'cs-confirm', 'cs-source',
    'cs-attr', 'cs-class', 'cs-ui-wizard', 'cs-ui-tree', 'cschart',
    'cstree', 'cswizard', 'csexpr-contains', 'csexpr-ternary',
    'cs-theme-tokens', 'cs-intent-register', 'cs-ui-register', 'cs-vm-chain',
    'cs-qs', 'cs-on', 'cs-batch',
    'cs-tree-event', 'cs-wizard-event'
  ];

  assert.strictEqual(Object.keys(snippets).length, 23);
  requiredPrefixes.forEach((prefix) => assert.ok(prefixes.has(prefix), `Missing VS Code snippet: ${prefix}`));

  const templateSnippetFile = path.join(workspaceRoot,
    'src/Templates/CodeSpiritVsixTemplate/ProjectTemplates/CodeSpirit.LibraryManagement/.vscode/codespirit.code-snippets');
  const templateSnippets = JSON.parse(fs.readFileSync(templateSnippetFile, 'utf8'));
  const templatePrefixes = new Set(Object.values(templateSnippets)
    .filter((snippet) => snippet && typeof snippet === 'object')
    .map((snippet) => snippet.prefix));
  ['cschart', 'cstree', 'cswizard', 'cs-tree-event', 'cs-wizard-event', 'cs-qs', 'cs-on', 'cs-batch'].forEach((prefix) => {
    assert.ok(templatePrefixes.has(prefix), `Missing template VS Code snippet: ${prefix}`);
  });

  // VSIX snippets are the Visual Studio counterpart of the project-level VS Code catalog.
  ['chart', 'tree', 'wizard'].forEach((name) => {
    const file = path.join(workspaceRoot, `src/Templates/CodeSpiritVsixTemplate/Snippets/codespirit-cs-${name}.snippet`);
    assert.ok(fs.existsSync(file), `Missing VSIX snippet: ${name}`);
    assert.ok(fs.readFileSync(file, 'utf8').includes(`<Shortcut>cs${name}</Shortcut>`));
  });
}

function validateTypeDeclarations() {
  const workspaceRoot = path.resolve(templateRoot, '../..');
  const files = [
    path.join(workspaceRoot, 'src/CodeSpirit.LibraryManagement/wwwroot/js/codespirit.d.ts'),
    path.join(workspaceRoot, 'src/Templates/CodeSpiritVsixTemplate/ProjectTemplates/CodeSpirit.LibraryManagement/wwwroot/js/codespirit.d.ts')
  ];
  const requiredText = [
    'interface CodespiritTreeToggleEventDetail',
    'interface CodespiritWizardStepEventDetail',
    'type CodespiritBuiltInUiBehavior',
    'qs<T extends Element = Element>',
    'batch(root: string | HTMLElement',
    "'codespirit:tree-toggle': CustomEvent<CodespiritTreeToggleEventDetail>",
    "'codespirit:wizard-step': CustomEvent<CodespiritWizardStepEventDetail>",
    'on<TEvent extends CodespiritEventType>',
    'CodespiritFieldRules'
  ];

  files.forEach((file) => {
    const content = fs.readFileSync(file, 'utf8');
    requiredText.forEach((text) => assert.ok(content.includes(text), `Missing d.ts declaration: ${text}`));
    assert.ok(!content.includes('CorespiritFieldRules'), 'Old field rules typo should not exist');
  });
}

function validateDevPanelAssets() {
  const workspaceRoot = path.resolve(templateRoot, '../..');
  const jsFiles = [
    path.join(workspaceRoot, 'src/CodeSpirit.LibraryManagement/wwwroot/js/ui/codespirit.devpanel.js'),
    path.join(workspaceRoot, 'src/Templates/CodeSpiritVsixTemplate/ProjectTemplates/CodeSpirit.LibraryManagement/wwwroot/js/ui/codespirit.devpanel.js')
  ];
  const cssFiles = [
    path.join(workspaceRoot, 'src/CodeSpirit.LibraryManagement/wwwroot/css/site.css'),
    path.join(workspaceRoot, 'src/Templates/CodeSpiritVsixTemplate/ProjectTemplates/CodeSpirit.LibraryManagement/wwwroot/css/site.css')
  ];
  const requiredJsText = [
    'cs-dev-filter',
    'cs-dev-inspect',
    'data-cs-show',
    'data-cs-enable',
    'data-cs-refresh',
    'data-cs-confirm',
    'data-cs-source',
    'data-ui',
    'undoEdit',
    'locateElement',
    'handlePagePick',
    'handleShortcuts',
    'cs-dev-diff',
    'cs-dev-edit-class-attr',
    'cs-dev-edit-style',
    'cs-dev-edit-text',
    'cs-dev-edit-html',
    'originalSnapshots',
    'applyPreviewBehaviors',
    'scheduleLivePreview',
    'sanitizeHtml',
    'localStorage'
  ];
  const requiredCssText = [
    '.cs-dev-item.active',
    '.cs-dev-tag.ui',
    '#cs-dev-filter',
    '#cs-dev-diff',
    '#cs-dev-undo',
    '#cs-dev-locate',
    'max-width: 360px',
    'flex-direction: column'
  ];

  const mainJs = fs.readFileSync(jsFiles[0], 'utf8');
  assert.strictEqual(mainJs, fs.readFileSync(jsFiles[1], 'utf8'), 'Template devpanel script must match source');
  jsFiles.forEach((file) => {
    const content = fs.readFileSync(file, 'utf8');
    requiredJsText.forEach((text) => assert.ok(content.includes(text), `Missing devpanel feature: ${text}`));
  });

  const mainCss = fs.readFileSync(cssFiles[0], 'utf8');
  assert.strictEqual(mainCss, fs.readFileSync(cssFiles[1], 'utf8'), 'Template site.css must match source');
  cssFiles.forEach((file) => {
    const content = fs.readFileSync(file, 'utf8');
    requiredCssText.forEach((text) => assert.ok(content.includes(text), `Missing devpanel style: ${text}`));
  });
}

function validateRuntimeCompatibilityLayer() {
  const workspaceRoot = path.resolve(templateRoot, '../..');
  const files = [
    path.join(workspaceRoot, 'src/CodeSpirit.LibraryManagement/wwwroot/js/codespirit.runtime.js'),
    path.join(workspaceRoot, 'src/Templates/CodeSpiritVsixTemplate/ProjectTemplates/CodeSpirit.LibraryManagement/wwwroot/js/codespirit.runtime.js')
  ];
  const requiredText = [
    'installCompatibilityLayer',
    'Array.from = function',
    'String.prototype.startsWith',
    'String.prototype.endsWith',
    'NodeList.prototype.forEach',
    'elementProto.matches',
    'elementProto.closest',
    'window.CustomEvent = function',
    'Fetch API is not available in this browser.',
    'rememberSubmitter',
    'var errors = {}',
    'changes[name] = value',
    'function qs(selector, root)',
    'function batch(root, changes)'
  ];

  const mainRuntime = fs.readFileSync(files[0], 'utf8');
  assert.strictEqual(mainRuntime, fs.readFileSync(files[1], 'utf8'), 'Template runtime script must match source');
  files.forEach((file) => {
    const content = fs.readFileSync(file, 'utf8');
    requiredText.forEach((text) => assert.ok(content.includes(text), `Missing runtime compatibility feature: ${text}`));
  });
}

function validateTemplateManifest() {
  const workspaceRoot = path.resolve(templateRoot, '../..');
  const manifest = fs.readFileSync(path.join(workspaceRoot,
    'src/Templates/CodeSpiritVsixTemplate/ProjectTemplates/CodeSpirit.LibraryManagement/CodeSpirit.LibraryManagement.vstemplate'), 'utf8');
  [
    'codespirit.runtime.js',
    'codespirit.expression.js',
    'jquery-lite.js',
    'jquery.behaviors.js',
    'ui.behaviors.js',
    'codespirit.intent.js',
    'codespirit.devpanel.js'
  ].forEach((file) => assert.ok(manifest.includes(file), `Missing template manifest item: ${file}`));
  ['LivePreview.aspx', 'LivePreviewViewModel.cs'].forEach((file) => {
    assert.ok(manifest.includes(file), `Missing live preview template manifest item: ${file}`);
  });

  const programFiles = [
    path.join(workspaceRoot, 'src/CodeSpirit.LibraryManagement/Program.cs'),
    path.join(workspaceRoot, 'src/Templates/CodeSpiritVsixTemplate/ProjectTemplates/CodeSpirit.LibraryManagement/Program.cs')
  ];
  ['data-cs-show', 'data-cs-enable', 'data-cs-refresh', 'data-cs-confirm', 'data-cs-source', 'data-cs-attr', 'data-cs-visible', 'data-cs-hidden', 'data-cs-enabled', 'data-cs-disabled', 'data-ui', 'style'].forEach((attr) => {
    programFiles.forEach((file) => assert.ok(fs.readFileSync(file, 'utf8').includes(attr), `Missing dev sync attribute ${attr} in ${file}`));
  });
}

async function main() {
  const document = new Document();
  const form = document.appendChild(new Element('form', { 'data-cs-vm': '', method: 'post', 'data-cs-vm-id': 'test-vm', name: 'TestVm' }));
  const city = form.appendChild(new Element('input', { name: 'City', 'data-cs-bind': 'City', value: 'Paris' }));
  const cityLabel = form.appendChild(new Element('span', { 'data-cs-bind': 'City' }));
  const token = form.appendChild(new Element('input', { name: 'Token', value: '' }));
  const refreshBtn = form.appendChild(new Element('button', { type: 'submit', 'data-cs-command': 'Refresh' }));
  const preDisabledBtn = form.appendChild(new Element('button', { type: 'submit', disabled: true, 'data-cs-command': 'SaveDraft' }));

  let fetchPayload = null;
  const context = vm.createContext({
    window: {
      location: { pathname: '/weather', hash: '' },
      history: { replaceState: function (_state, _title, url) { this.lastUrl = url; } },
      CSS: { escape: (value) => String(value) },
      confirm: () => false
    },
    document,
    FormData: FormDataStub,
    Event,
    CustomEvent: Event,
    console,
    setTimeout,
    clearTimeout,
    fetch: async (url, options) => {
      fetchPayload = { url, options: { ...options, body: JSON.parse(options.body) } };
      return {
        ok: true,
        status: 200,
        headers: { get: () => 'application/json' },
        text: async () => JSON.stringify({ state: { City: 'Rome' } })
      };
    }
  });
  context.window.document = document;
  context.window.FormData = FormDataStub;

  runScript('wwwroot/js/codespirit.runtime.js', context);
  runScript('wwwroot/js/codespirit.expression.js', context);
  context.CodeSpirit = context.window.CodeSpirit;

  // ---- Exposed API ----
  assert.strictEqual(typeof context.window.CodeSpirit.vm, 'function');
  assert.strictEqual(typeof context.window.CodeSpirit.VmChain, 'function');
  assert.strictEqual(typeof context.window.CodeSpirit.applyErrors, 'function');
  assert.strictEqual(typeof context.window.CodeSpirit.clearErrors, 'function');
  assert.strictEqual(typeof context.window.CodeSpirit.expression.evaluate, 'function');
  assert.strictEqual(context.window.$cs, context.window.CodeSpirit);
  assert.strictEqual(context.window.CS, undefined);

  // ---- Occupied alias ----
  const occupiedAlias = { existing: true };
  const occupiedDocument = new Document();
  const occupiedContext = vm.createContext({
    window: { $cs: occupiedAlias, location: { pathname: '/weather' } },
    document: occupiedDocument,
    FormData: FormDataStub,
    Event,
    CustomEvent: Event,
    fetch: context.fetch
  });
  occupiedContext.window.document = occupiedDocument;
  runScript('wwwroot/js/codespirit.runtime.js', occupiedContext);
  assert.strictEqual(occupiedContext.window.$cs, occupiedAlias);
  assert.strictEqual(typeof occupiedContext.window.CodeSpirit.vm, 'function');

  // ---- Expression engine ----
  const bookCount = form.appendChild(new Element('input', { name: 'BookCount', 'data-cs-bind': 'BookCount', value: '0' }));
  const hasBooksPanel = form.appendChild(new Element('section', { 'data-cs-show': 'BookCount > 0' }));
  const actionButton = form.appendChild(new Element('button', { 'data-cs-enable': "BookCount > 0 && City contains 'Paris'" }));

  context.window.CodeSpirit.expression.apply(form);
  assert.strictEqual(hasBooksPanel.style.display, 'none');
  assert.strictEqual(actionButton.disabled, true);

  bookCount.value = '2';
  context.window.CodeSpirit.input(bookCount, 'BookCount', '2');
  assert.strictEqual(hasBooksPanel.style.display, '');
  assert.strictEqual(actionButton.disabled, false);
  assert.strictEqual(context.window.CodeSpirit.expression.evaluate("BookCount > 1 ? 'many' : 'few'", form), 'many');

  const dangerButton = form.appendChild(new Element('button', { 'data-cs-confirm': 'Delete this book?' }));
  const confirmClick = new Event('click', { bubbles: true });
  dangerButton.dispatchEvent(confirmClick);
  assert.strictEqual(confirmClick.defaultPrevented, true);

  const category = form.appendChild(new Element('select', { name: 'Category', 'data-cs-source': 'LoadCategories', 'data-cs-source-field': 'CategoryOptions' }));
  context.fetch = async (url, options) => {
    fetchPayload = { url, options: { ...options, body: JSON.parse(options.body) } };
    return {
      ok: true,
      status: 200,
      headers: { get: () => 'application/json' },
      text: async () => JSON.stringify({ state: { CategoryOptions: [{ Value: 'Sci', Text: 'Science' }, { Value: 'Art', Text: 'Art' }] } })
    };
  };
  context.window.CodeSpirit.expression.apply(form);
  for (var i = 0; i < 10; i++) { await Promise.resolve(); }
  assert.strictEqual(category.children.length, 2);
  assert.strictEqual(category.children[0].value, 'Sci');
  assert.strictEqual(category.children[0].textContent, 'Science');

  // ---- Chain initialization ----
  var chain = context.window.CodeSpirit.vm(form);
  assert.ok(chain instanceof context.window.CodeSpirit.VmChain);
  assert.strictEqual(chain._form, form);
  assert.strictEqual(chain._destroyed, false);

  // ---- .set() / .get() / .state() ----
  chain.set('City', 'Oslo');
  assert.strictEqual(city.value, 'Oslo');
  assert.strictEqual(cityLabel.textContent, 'Oslo');
  assert.strictEqual(chain.get('City'), 'Oslo');

  chain.set({ City: 'Stockholm', Token: 'x' });
  assert.strictEqual(city.value, 'Stockholm');
  assert.strictEqual(chain.get('City'), 'Stockholm');

  const specialName = form.appendChild(new Element('input', { name: 'Field]Name', 'data-cs-bind': 'Field]Name', value: '' }));
  chain.set('Field]Name', 'escaped');
  assert.strictEqual(specialName.value, 'escaped');

  var snap = chain.state();
  assert.strictEqual(snap.City, 'Stockholm');
  assert.strictEqual(snap.Token, 'x');

  // ---- .val() getter form ----
  assert.strictEqual(chain.val('City'), 'Stockholm');

  // ---- chaining: set returns this ----
  assert.strictEqual(chain.set('City', 'Copenhagen'), chain);

  // ---- .on() / .off() / .once() return this ----
  assert.strictEqual(chain.on('codespirit:updated', function () {}), chain);
  assert.strictEqual(chain.once('codespirit:loaded', function () {}), chain);

  // ---- .el() / .all() scoped queries ----
  assert.strictEqual(chain.el('[name="City"]'), city);
  assert.ok(chain.all('[name="City"]').length >= 1);

  // ---- .invoke() returns a Promise ----
  var invoked = chain.invoke('Refresh');
  assert.ok(invoked && typeof invoked.then === 'function');

  // ---- Reset city for follow-up tests ----
  city.value = 'Paris';
  cityLabel.textContent = 'Paris';

  let changed = null;
  form.addEventListener('codespirit:changed', (event) => {
    changed = event.detail;
  });

  city.value = 'Berlin';
  city.dispatchEvent(new Event('input', { bubbles: true }));
  assert.strictEqual(cityLabel.textContent, 'Berlin');
  assert.strictEqual(changed.name, 'City');
  assert.strictEqual(changed.value, 'Berlin');

  context.window.CodeSpirit.input(city, 'City', 'Tokyo');
  assert.strictEqual(city.value, 'Tokyo');
  assert.strictEqual(cityLabel.textContent, 'Tokyo');

  // ---- Submit with loading state ----
  const updated = new Promise((resolve) => form.addEventListener('codespirit:updated', resolve));
  const region = document.appendChild(new Element('section', { 'data-cs-region': 'forecast' }));
  region.textContent = 'Old Forecast';
  const regionWidget = region.appendChild(new Element('div', { 'data-ui': 'custom-widget' }));
  regionWidget.textContent = 'Old Widget';
  const submit = new Event('submit', { bubbles: true });
  submit.submitter = refreshBtn;

  context.fetch = async (url, options) => {
    fetchPayload = { url, options: { ...options, body: JSON.parse(options.body) } };
    return {
      ok: true,
      status: 200,
      headers: { get: () => 'application/json' },
      text: async () => JSON.stringify({
        state: { City: 'Rome' },
        regions: { forecast: '<section data-cs-region="forecast"><div data-ui="custom-widget">Fresh Forecast</div></section>' }
      })
    };
  };
  form.dispatchEvent(submit);
  assert.ok(form.classList.contains('cs-loading'));
  assert.ok(form.getAttribute('data-cs-busy') !== null);
  assert.strictEqual(refreshBtn.disabled, true);
  assert.strictEqual(preDisabledBtn.disabled, true);
  await updated;
  assert.strictEqual(submit.defaultPrevented, true);
  assert.strictEqual(fetchPayload.url, '/weather');
  assert.strictEqual(fetchPayload.options.body.City, 'Tokyo');
  assert.strictEqual(fetchPayload.options.body.__command, 'Refresh');
  assert.strictEqual(city.value, 'Rome');
  assert.strictEqual(cityLabel.textContent, 'Rome');
  assert.strictEqual(document.querySelector('[data-cs-region="forecast"]').textContent, 'Fresh Forecast');
  assert.ok(!form.classList.contains('cs-loading'));
  assert.strictEqual(form.getAttribute('data-cs-busy'), null);
  assert.strictEqual(refreshBtn.disabled, false);
  assert.strictEqual(preDisabledBtn.disabled, true);

  form.appendChild(new Element('section', { 'data-cs-region': 'multi' }));
  context.window.CodeSpirit.applyRegions(form, {
    multi: '<div data-region-item="first">First</div><div data-region-item="second">Second</div>'
  });
  var multiRegion = document.querySelector('[data-cs-region="multi"]');
  assert.strictEqual(multiRegion.children.length, 2);
  assert.strictEqual(multiRegion.children[0].textContent, 'First');
  assert.strictEqual(multiRegion.children[1].textContent, 'Second');

  // ---- .reset() ----
  chain.set('City', 'Modified');
  chain.set('Token', 'modified');
  chain.reset();
  assert.strictEqual(chain.get('City'), 'Paris');
  assert.strictEqual(chain.get('Token'), '');
  assert.ok(!form.classList.contains('cs-has-errors'));

  let resetEvent = null;
  form.addEventListener('codespirit:reset', (e) => { resetEvent = e; });
  chain.set('City', 'X');
  chain.reset(['City']);
  assert.strictEqual(chain.get('City'), 'Paris');
  assert.ok(resetEvent !== null);

  // ---- .destroy() ----
  chain.destroy();
  assert.strictEqual(chain._destroyed, true);
  try {
    chain.get('City');
    assert.fail('should throw after destroy');
  } catch (e) {
    assert.ok(e.message.indexOf('destroyed') >= 0);
  }
  try {
    chain.set('City', 'test');
    assert.fail('should throw after destroy');
  } catch (e) {
    assert.ok(e.message.indexOf('destroyed') >= 0);
  }

  // ---- New chain after destroy ----
  var chain2 = context.window.CodeSpirit.vm(form);
  assert.ok(chain2._destroyed === false);
  assert.strictEqual(chain2.get('City'), 'Paris');

  // ---- .observe() ----
  var observedFields = [];
  chain2.observe('City', function (value, field) {
    observedFields.push({ value, field });
  });
  chain2.set('City', 'London');
  assert.strictEqual(observedFields.length, 1);
  assert.strictEqual(observedFields[0].field, 'City');
  assert.strictEqual(observedFields[0].value, 'London');

  var observedMulti = [];
  chain2.observe(['City', 'Token'], function (value, field) {
    observedMulti.push({ value, field });
  });
  chain2.set({ City: 'Dublin', Token: 'abc' });
  assert.ok(observedMulti.length >= 2);

  // ---- observe all fields (empty array) ----
  var observedAll = [];
  chain2.observe([], function (value, field) {
    observedAll.push({ value, field });
  });
  chain2.set('City', 'Berlin');
  assert.ok(observedAll.length >= 1);

  // ---- applyErrors / clearErrors ----
  context.window.CodeSpirit.applyErrors(form, { City: 'City is required', Token: 'Invalid token' });
  assert.ok(city.classList.contains('cs-invalid'));
  assert.ok(token.classList.contains('cs-invalid'));
  assert.ok(form.classList.contains('cs-has-errors'));

  var validationEvent = null;
  form.addEventListener('codespirit:validation', (e) => { validationEvent = e; });

  context.window.CodeSpirit.clearErrors(form);
  assert.ok(!city.classList.contains('cs-invalid'));
  assert.ok(!token.classList.contains('cs-invalid'));
  assert.ok(!form.classList.contains('cs-has-errors'));

  // ---- applyErrors with validation event ----
  context.window.CodeSpirit.applyErrors(form, { City: 'Too short' });
  assert.ok(validationEvent !== null);
  assert.strictEqual(validationEvent.detail.errors.City, 'Too short');
  context.window.CodeSpirit.clearErrors(form);

  // ---- Submit with server-side errors ----
  chain2.set('City', 'Test');
  var errorSubmit = new Event('submit', { bubbles: true });
  errorSubmit.submitter = refreshBtn;

  var errorPromise = new Promise((resolve) => form.addEventListener('codespirit:error', resolve));
  context.fetch = async (url, options) => {
    return {
      ok: true,
      status: 200,
      headers: { get: () => 'application/json' },
      text: async () => JSON.stringify({
        errors: { City: 'Server validation failed' }
      })
    };
  };
  form.dispatchEvent(errorSubmit);
  var errorResult = await errorPromise;
  assert.strictEqual(errorResult.detail.City, 'Server validation failed');
  assert.ok(city.classList.contains('cs-invalid'));
  assert.ok(form.classList.contains('cs-has-errors'));
  assert.ok(!form.classList.contains('cs-loading'));
  context.window.CodeSpirit.clearErrors(form);

  // ---- Submit with HTTP error (non-ok response) ----
  var httpErrorSubmit = new Event('submit', { bubbles: true });
  httpErrorSubmit.submitter = refreshBtn;

  var httpErrorPromise = new Promise((resolve) => form.addEventListener('codespirit:error', resolve));
  context.fetch = async (url, options) => {
    return {
      ok: false,
      status: 500,
      text: async () => 'Internal Server Error'
    };
  };
  form.dispatchEvent(httpErrorSubmit);
  var httpError = await httpErrorPromise;
  assert.strictEqual(httpError.detail.status, 500);
  assert.ok(!form.classList.contains('cs-loading'));

  // ---- data-cs-visible binding ----
  const visibleDiv = form.appendChild(new Element('div', { 'data-cs-visible': 'IsActive' }));
  const hiddenDiv = form.appendChild(new Element('div', { 'data-cs-hidden': 'IsActive' }));
  const visibleExact = form.appendChild(new Element('div', { 'data-cs-visible': 'IsActive:true' }));
  const isActiveInput = form.appendChild(new Element('input', { name: 'IsActive', value: 'false' }));

  chain2.set('IsActive', 'true');
  assert.strictEqual(visibleDiv.style.display, '');
  assert.strictEqual(hiddenDiv.style.display, 'none');
  assert.strictEqual(visibleExact.style.display, '');

  chain2.set('IsActive', 'false');
  assert.strictEqual(visibleDiv.style.display, 'none');
  assert.strictEqual(hiddenDiv.style.display, '');
  assert.strictEqual(visibleExact.style.display, 'none');

  // ---- data-cs-class binding ----
  const classDiv = form.appendChild(new Element('div', { 'data-cs-class': 'IsActive:active' }));
  const classDivDefault = form.appendChild(new Element('div', { 'data-cs-class': 'IsActive' }));

  chain2.set('IsActive', 'true');
  assert.ok(classDiv.classList.contains('active'));
  assert.ok(classDivDefault.classList.contains('isactive'));

  chain2.set('IsActive', 'false');
  assert.ok(!classDiv.classList.contains('active'));
  assert.ok(!classDivDefault.classList.contains('isactive'));

  // ---- invoke with errors in result ----
  context.fetch = async (url, options) => {
    return {
      ok: true,
      status: 200,
      headers: { get: () => 'application/json' },
      text: async () => JSON.stringify({
        errors: { City: 'City too short' },
        state: { City: 'Test' }
      })
    };
  };

  var invokeResult = await chain2.invoke('Refresh');
  assert.strictEqual(invokeResult.errors.City, 'City too short');
  assert.ok(city.classList.contains('cs-invalid'));

  context.window.CodeSpirit.clearErrors(form);

  // ---- ui.behaviors.js integration ----
  runScript('wwwroot/js/ui/ui.behaviors.js', context);
  assert.strictEqual(typeof context.window.CodeSpirit.mount, 'function');
  assert.strictEqual(typeof context.window.CodeSpirit.refresh, 'function');

  runScript('wwwroot/js/ui/codespirit.intent.js', context);
  assert.strictEqual(typeof context.window.CodeSpirit.intent.analyze, 'function');

  document.title = 'Library Admin Dashboard';
  const sceneRoot = document.appendChild(new Element('main', { class: 'library-screen' }));
  sceneRoot.appendChild(new Element('h1')).textContent = 'Library Admin Dashboard';
  sceneRoot.appendChild(new Element('button', { 'data-cs-command': 'BorrowBook' })).textContent = 'Borrow Book';
  sceneRoot.appendChild(new Element('th')).textContent = 'ISBN';
  context.window.CodeSpirit.intent.analyze(sceneRoot);
  assert.ok(sceneRoot.classList.contains('cs-scene-library'));
  assert.strictEqual(sceneRoot.getAttribute('data-cs-scene-current'), 'library');

  const explicitSceneRoot = document.appendChild(new Element('section', { 'data-cs-scene': 'dashboard' }));
  explicitSceneRoot.appendChild(new Element('h1')).textContent = 'Realtime 大屏';
  context.window.CodeSpirit.intent.analyze(explicitSceneRoot);
  assert.ok(explicitSceneRoot.classList.contains('cs-scene-dashboard'));
  assert.strictEqual(explicitSceneRoot.getAttribute('data-cs-scene-current'), 'dashboard');

  document.title = 'Finance Billing Report';
  const financeRoot = document.appendChild(new Element('main'));
  financeRoot.appendChild(new Element('h1')).textContent = 'Finance Billing Report';
  financeRoot.appendChild(new Element('label')).textContent = 'Invoice Amount';
  financeRoot.appendChild(new Element('button')).textContent = 'Collect Payment';
  context.window.CodeSpirit.intent.analyze(financeRoot);
  assert.ok(financeRoot.classList.contains('cs-scene-finance'));

  document.title = 'Patient Appointment Clinic';
  const healthcareRoot = document.appendChild(new Element('main'));
  healthcareRoot.appendChild(new Element('h1')).textContent = 'Patient Appointment Clinic';
  healthcareRoot.appendChild(new Element('label')).textContent = 'Doctor Diagnosis';
  context.window.CodeSpirit.intent.analyze(healthcareRoot);
  assert.ok(healthcareRoot.classList.contains('cs-scene-healthcare'));

  document.title = 'HR Employee Recruiting';
  const hrRoot = document.appendChild(new Element('main'));
  hrRoot.appendChild(new Element('h1')).textContent = 'HR Employee Recruiting';
  hrRoot.appendChild(new Element('label')).textContent = 'Candidate Payroll Attendance';
  context.window.CodeSpirit.intent.analyze(hrRoot);
  assert.ok(hrRoot.classList.contains('cs-scene-hr'));

  document.title = 'Factory Production Work Order';
  const manufacturingRoot = document.appendChild(new Element('main'));
  manufacturingRoot.appendChild(new Element('h1')).textContent = 'Manufacturing Production';
  manufacturingRoot.appendChild(new Element('label')).textContent = 'Work Order Quality OEE';
  context.window.CodeSpirit.intent.analyze(manufacturingRoot);
  assert.ok(manufacturingRoot.classList.contains('cs-scene-manufacturing'));

  document.title = 'Support Ticket Helpdesk';
  const supportRoot = document.appendChild(new Element('main'));
  supportRoot.appendChild(new Element('h1')).textContent = 'Support Ticket Helpdesk';
  supportRoot.appendChild(new Element('label')).textContent = 'SLA Incident Queue';
  context.window.CodeSpirit.intent.analyze(supportRoot);
  assert.ok(supportRoot.classList.contains('cs-scene-support'));

  const additionalScenes = [
    ['Commerce Orders', 'Product Cart Payment', 'commerce'],
    ['Content CMS Publish', 'Article Draft Author', 'content'],
    ['Analytics Report Growth', 'Conversion Statistics Segment', 'analytics'],
    ['CRM Customer Pipeline', 'Lead Opportunity Deal', 'crm'],
    ['Education Course Exam', 'Student Lesson Grade', 'education'],
    ['Logistics Shipment Delivery', 'Warehouse Fleet Route', 'logistics'],
    ['Developer API Webhook', 'SDK Deployment Logs', 'developer'],
    ['Hotel Guest Booking', 'Room Check-in Reservation', 'hospitality'],
    ['Real Estate Property', 'Listing Lease Tenant', 'real-estate'],
    ['Legal Contract Case', 'Compliance Clause Risk', 'legal']
  ];
  additionalScenes.forEach(([title, label, expected]) => {
    document.title = title;
    const root = document.appendChild(new Element('main'));
    root.appendChild(new Element('h1')).textContent = title;
    root.appendChild(new Element('label')).textContent = label;
    context.window.CodeSpirit.intent.analyze(root);
    assert.ok(root.classList.contains(`cs-scene-${expected}`), `${expected} scene should be detected`);
  });

  const extendedScenes = [
    ['Supply Chain Procurement', 'Vendor Supplier Inventory', 'supply-chain'],
    ['Research Lab Study', 'Experiment Sample Publication', 'research'],
    ['Security Audit Threat', 'Vulnerability Intrusion Firewall', 'security'],
    ['Retail Store POS', 'Checkout Customer Loyalty', 'retail'],
    ['Insurance Policy Claim', 'Premium Underwriting Coverage', 'insurance'],
    ['NGO Charity Donation', 'Volunteer Grant Community', 'ngo']
  ];
  extendedScenes.forEach(([title, label, expected]) => {
    document.title = title;
    const root = document.appendChild(new Element('main'));
    root.appendChild(new Element('h1')).textContent = title;
    root.appendChild(new Element('label')).textContent = label;
    context.window.CodeSpirit.intent.analyze(root);
    assert.ok(root.classList.contains(`cs-scene-${expected}`), `${expected} extended scene should be detected`);
  });

  const newScenes = [
    ['Telecom Network Signal', 'Subscriber SIM Roaming', 'telecom'],
    ['Energy Power Grid', 'Electricity Solar Wind', 'energy'],
    ['Transportation Bus Metro', 'Route Schedule Fare', 'transportation'],
    ['Agriculture Farm Crop', 'Harvest Irrigation Soil', 'agriculture'],
    ['Media News Broadcast', 'Video Streaming Channel', 'media'],
    ['Gaming Player Rank', 'Match Score Level', 'gaming'],
    ['Automotive Vehicle Dealer', 'Maintenance Repair Parts', 'automotive'],
    ['Pharmaceutical Drug Pharmacy', 'Prescription Dosage Tablet', 'pharmaceutical'],
    ['Construction Building Project', 'Blueprint Material Concrete', 'construction'],
    ['Aviation Flight Airport', 'Boarding Gate Terminal', 'aviation'],
    ['Maritime Shipping Port', 'Container Dock Freight', 'maritime'],
    ['Government Agency Department', 'Permit License Public', 'government']
  ];
  newScenes.forEach(([title, label, expected]) => {
    document.title = title;
    const root = document.appendChild(new Element('main'));
    root.appendChild(new Element('h1')).textContent = title;
    root.appendChild(new Element('label')).textContent = label;
    context.window.CodeSpirit.intent.analyze(root);
    assert.ok(root.classList.contains(`cs-scene-${expected}`), `${expected} new scene should be detected`);
  });

  document.title = 'Candidate';
  const lowConfidenceRoot = document.appendChild(new Element('main'));
  lowConfidenceRoot.appendChild(new Element('h1')).textContent = 'Candidate';
  context.window.CodeSpirit.intent.analyze(lowConfidenceRoot);
  assert.ok(lowConfidenceRoot.classList.contains('cs-scene-app'));
  assert.strictEqual(lowConfidenceRoot.getAttribute('data-cs-scene-confidence'), 'low');
  assert.strictEqual(lowConfidenceRoot.getAttribute('data-cs-scene-candidate'), 'hr');

  assert.strictEqual(typeof context.window.CodeSpirit.theme.exportTokens, 'function');
  const tokens = context.window.CodeSpirit.theme.exportTokens(sceneRoot);
  assert.strictEqual(tokens['--cs-primary'], '#6d5dfc');
  assert.ok(tokens['--cs-font'].indexOf('Inter') >= 0);

  context.window.CodeSpirit.mount(document);

  let customBehaviorCount = 0;
  context.window.CodeSpirit.ui.register('custom-widget', (elements) => {
    customBehaviorCount += elements.length;
    context.window.CodeSpirit.ui.ready(elements, 'custom-widget');
  });
  const customWidget = form.appendChild(new Element('div', { 'data-ui': 'custom-widget' }));
  const patchedWidget = document.querySelector('[data-cs-region="forecast"]').querySelector('[data-ui="custom-widget"]');
  context.window.CodeSpirit.refresh(document);
  context.window.CodeSpirit.refresh(document);
  assert.strictEqual(patchedWidget.getAttribute('data-ui-ready'), 'custom-widget');
  assert.strictEqual(customWidget.getAttribute('data-ui-ready'), 'custom-widget');
  assert.strictEqual(customBehaviorCount, 2);

  // Verify built-in datepicker behavior
  const dateInput = document.createElement('input');
  dateInput.setAttribute('data-ui', 'datepicker');
  const dateWidget = form.appendChild(dateInput);
  context.window.CodeSpirit.mount(document);
  assert.strictEqual(dateWidget.getAttribute('type'), 'date');
  assert.ok(dateWidget.getAttribute('data-ui-ready').split(/\s+/).indexOf('datepicker') >= 0);

  // Verify confirm-click behavior
  const confirmBtn = document.createElement('button');
  confirmBtn.setAttribute('data-ui', 'confirm-click');
  confirmBtn.setAttribute('data-confirm', 'Proceed?');
  form.appendChild(confirmBtn);
  context.window.CodeSpirit.mount(document);
  assert.ok(confirmBtn.getAttribute('data-ui-ready').split(/\s+/).indexOf('confirm-click') >= 0);

  // Verify auto-submit behavior
  const autoField = document.createElement('input');
  autoField.setAttribute('data-ui', 'auto-submit');
  autoField.setAttribute('data-debounce', '500');
  form.appendChild(autoField);
  context.window.CodeSpirit.mount(document);
  assert.ok(autoField.getAttribute('data-ui-ready').split(/\s+/).indexOf('auto-submit') >= 0);

  // Verify tabs behavior
  const tabs = form.appendChild(new Element('div', { class: 'cs-tabs', 'data-ui': 'tabs' }));
  const overviewTab = tabs.appendChild(new Element('a', { class: 'cs-tab active', href: '#overview', role: 'tab' }));
  const detailsTab = tabs.appendChild(new Element('a', { class: 'cs-tab', href: '#details', role: 'tab' }));
  const overviewPanel = tabs.appendChild(new Element('section', { id: 'overview', class: 'cs-tab-panel active' }));
  const detailsPanel = tabs.appendChild(new Element('section', { id: 'details', class: 'cs-tab-panel' }));
  context.window.CodeSpirit.mount(document);
  detailsTab.dispatchEvent(new Event('click', { bubbles: true }));
  assert.ok(!overviewTab.classList.contains('active'));
  assert.ok(detailsTab.classList.contains('active'));
  assert.ok(!overviewPanel.classList.contains('active'));
  assert.ok(detailsPanel.classList.contains('active'));
  assert.strictEqual(detailsTab.getAttribute('aria-selected'), 'true');

  // Verify wizard behavior
  const wizard = form.appendChild(new Element('div', { class: 'cs-wizard', 'data-ui': 'wizard', 'data-cs-wizard': '' }));
  const firstWizardStep = wizard.appendChild(new Element('button', { class: 'cs-wizard-step active', 'data-cs-wizard-step': 'one', 'aria-selected': 'true' }));
  const secondWizardStep = wizard.appendChild(new Element('button', { class: 'cs-wizard-step', 'data-cs-wizard-step': 'two', 'aria-selected': 'false' }));
  const firstWizardPanel = wizard.appendChild(new Element('div', { class: 'cs-wizard-panel active', 'data-cs-wizard-panel': 'one' }));
  const secondWizardPanel = wizard.appendChild(new Element('div', { class: 'cs-wizard-panel', 'data-cs-wizard-panel': 'two' }));
  secondWizardPanel.style.display = 'none';
  let wizardEventStep = null;
  wizard.addEventListener('codespirit:wizard-step', function (event) {
    wizardEventStep = event.detail.step;
  });
  context.window.CodeSpirit.mount(document);
  secondWizardStep.dispatchEvent(new Event('click', { bubbles: true }));
  assert.ok(!firstWizardStep.classList.contains('active'));
  assert.ok(secondWizardStep.classList.contains('active'));
  assert.strictEqual(firstWizardStep.getAttribute('aria-selected'), 'false');
  assert.strictEqual(secondWizardStep.getAttribute('aria-selected'), 'true');
  assert.strictEqual(firstWizardPanel.style.display, 'none');
  assert.strictEqual(secondWizardPanel.style.display, '');
  assert.strictEqual(wizardEventStep, 'two');

  // Verify tree behavior
  const tree = form.appendChild(new Element('section', { class: 'cs-card cs-tree', 'data-ui': 'tree' }));
  const treeRoot = tree.appendChild(new Element('ul', { class: 'cs-tree-root', 'data-cs-tree': '' }));
  const treeNode = treeRoot.appendChild(new Element('li', { class: 'cs-tree-node cs-tree-node-has-children collapsed', 'data-cs-tree-value': 'root' }));
  const treeToggle = treeNode.appendChild(new Element('button', { class: 'cs-tree-toggle', 'data-cs-tree-toggle': '', 'aria-expanded': 'false' }));
  treeToggle.appendChild(new Element('span', { class: 'cs-tree-label' })).textContent = 'Root';
  const treeChildren = treeNode.appendChild(new Element('ul', { class: 'cs-tree-children' }));
  treeChildren.style.display = 'none';
  let treeToggleDetail = null;
  tree.addEventListener('codespirit:tree-toggle', function (event) {
    treeToggleDetail = event.detail;
  });
  context.window.CodeSpirit.mount(document);
  treeToggle.dispatchEvent(new Event('click', { bubbles: true }));
  assert.strictEqual(treeToggle.getAttribute('aria-expanded'), 'true');
  assert.ok(!treeNode.classList.contains('collapsed'));
  assert.strictEqual(treeChildren.style.display, '');
  assert.strictEqual(treeToggleDetail.value, 'root');
  assert.strictEqual(treeToggleDetail.expanded, true);

  // Verify modal behavior
  const openModal = form.appendChild(new Element('button', { 'data-modal-target': '#edit-modal' }));
  const modal = form.appendChild(new Element('div', { id: 'edit-modal', class: 'cs-modal', 'data-ui': 'modal', hidden: '' }));
  const closeModal = modal.appendChild(new Element('button', { 'data-modal-close': '' }));
  context.window.CodeSpirit.mount(document);
  openModal.dispatchEvent(new Event('click', { bubbles: true }));
  assert.strictEqual(modal.hasAttribute('hidden'), false);
  closeModal.dispatchEvent(new Event('click', { bubbles: true }));
  assert.strictEqual(modal.hasAttribute('hidden'), true);
  modal.removeAttribute('hidden');
  document.dispatchEvent(new Event('keydown', { key: 'Escape' }));
  assert.strictEqual(modal.hasAttribute('hidden'), true);

  // ---- data-cs-attr binding ----
  var linkEl = form.appendChild(new Element('a', { 'data-cs-attr': 'DetailUrl:href' }));
  chain2.set('DetailUrl', '/details/42');
  assert.strictEqual(linkEl.getAttribute('href'), '/details/42');
  chain2.set('DetailUrl', '');
  assert.strictEqual(linkEl.getAttribute('href'), null);

  var imgEl = form.appendChild(new Element('img', { 'data-cs-attr': 'AvatarUrl:src' }));
  chain2.set('AvatarUrl', '/img/avatar.png');
  assert.strictEqual(imgEl.getAttribute('src'), '/img/avatar.png');

  // ---- data-cs-enabled / data-cs-disabled binding ----
  var enabledInput = form.appendChild(new Element('input', { 'data-cs-enabled': 'CanEdit', disabled: true }));
  chain2.set('CanEdit', 'true');
  assert.strictEqual(enabledInput.disabled, false);
  chain2.set('CanEdit', 'false');
  assert.strictEqual(enabledInput.disabled, true);

  var disabledBtn = form.appendChild(new Element('button', { 'data-cs-disabled': 'IsLocked' }));
  chain2.set('IsLocked', 'true');
  assert.strictEqual(disabledBtn.disabled, true);
  chain2.set('IsLocked', 'false');
  assert.strictEqual(disabledBtn.disabled, false);

  // ---- chain.submit() ----
  var submitCalled = false;
  form.requestSubmit = function () {
    submitCalled = true;
  };
  chain2.submit();
  assert.strictEqual(submitCalled, true);

  // ---- chain.validate() ----
  var chain3 = context.window.CodeSpirit.vm(form);
  chain3.set('City', '');
  var valid = chain3.validate({
    City: { required: true, message: 'City required' }
  });
  assert.strictEqual(valid, false);
  assert.ok(city.classList.contains('cs-invalid'));
  context.window.CodeSpirit.clearErrors(form);

  chain3.set('City', 'Ab');
  valid = chain3.validate({
    City: { minLength: 3, message: 'Too short' }
  });
  assert.strictEqual(valid, false);
  context.window.CodeSpirit.clearErrors(form);

  chain3.set('City', 'Paris');
  valid = chain3.validate({
    City: { required: true, minLength: 3 }
  });
  assert.strictEqual(valid, true);
  assert.ok(!city.classList.contains('cs-invalid'));

  valid = chain3.validate({
    City: { pattern: /^[A-Z]/, message: 'Must start with capital' }
  });
  assert.strictEqual(valid, true);

  // Custom validator
  valid = chain3.validate({
    City: { custom: function (val) { return val === 'Paris' || 'Must be Paris'; } }
  });
  assert.strictEqual(valid, true);

  valid = chain3.validate({
    City: { custom: function (val) { return false; } }
  });
  assert.strictEqual(valid, false);
  context.window.CodeSpirit.clearErrors(form);

  token.setAttribute('name', 'Token');
  token.value = '';
  chain3.set('City', '');
  valid = chain3.validate({
    City: { required: true, message: 'City required' },
    Token: { required: true, message: 'Token required' }
  });
  assert.strictEqual(valid, false);
  assert.ok(city.classList.contains('cs-invalid'));
  assert.ok(token.classList.contains('cs-invalid'));
  context.window.CodeSpirit.clearErrors(form);

  // ---- New validation rules (url, numeric, integer, phone, matches) ----
  var webInput = form.appendChild(new Element('input', { name: 'Website' }));
  var countInput = form.appendChild(new Element('input', { name: 'Count' }));
  var telInput = form.appendChild(new Element('input', { name: 'Phone' }));
  var confirmCity = form.appendChild(new Element('input', { name: 'ConfirmCity' }));

  chain3.set('Website', 'not_a_valid_url');
  valid = chain3.validate({ Website: { url: true } });
  assert.strictEqual(valid, false);
  chain3.set('Website', 'https://example.com');
  valid = chain3.validate({ Website: { url: true } });
  assert.strictEqual(valid, true);

  chain3.set('Count', '3.14');
  valid = chain3.validate({ Count: { integer: true } });
  assert.strictEqual(valid, false);
  chain3.set('Count', '42');
  valid = chain3.validate({ Count: { integer: true, numeric: true } });
  assert.strictEqual(valid, true);

  chain3.set('Phone', 'not-a-phone');
  valid = chain3.validate({ Phone: { phone: true } });
  assert.strictEqual(valid, false);
  chain3.set('Phone', '+86-13800138000');
  valid = chain3.validate({ Phone: { phone: true } });
  assert.strictEqual(valid, true);

  chain3.set('City', 'Tokyo');
  chain3.set('ConfirmCity', 'Kyoto');
  valid = chain3.validate({ ConfirmCity: { matches: 'City' } });
  assert.strictEqual(valid, false);
  chain3.set('ConfirmCity', 'Tokyo');
  valid = chain3.validate({ ConfirmCity: { matches: 'City' } });
  assert.strictEqual(valid, true);
  context.window.CodeSpirit.clearErrors(form);

  // ---- Global validator registry ----
  assert.strictEqual(typeof context.window.CodeSpirit.registerValidator, 'function');
  context.window.CodeSpirit.registerValidator('even', function (val) {
    return parseInt(val, 10) % 2 === 0 || 'Must be even';
  });
  chain3.set('Count', '3');
  valid = chain3.validate({ Count: { even: true } });
  assert.strictEqual(valid, false);
  chain3.set('Count', '4');
  valid = chain3.validate({ Count: { even: true } });
  assert.strictEqual(valid, true);
  context.window.CodeSpirit.clearErrors(form);

  // ---- Loading button and debounce attribute checks ----
  var loadingBtn = form.appendChild(new Element('button', {
    type: 'submit',
    'data-cs-command': 'Save',
    'data-cs-loading': 'true',
    'data-cs-debounce': '300'
  }));
  assert.strictEqual(loadingBtn.getAttribute('data-cs-loading'), 'true');
  assert.strictEqual(loadingBtn.getAttribute('data-cs-debounce'), '300');

  runScript('wwwroot/js/ui/codespirit.intent.js', context);
  context.CodeSpirit = context.window.CodeSpirit;
  runScript('wwwroot/js/tests/runtime.test.js', context);
  validateSnippetCatalog();
  validateTypeDeclarations();
  validateDevPanelAssets();
  validateRuntimeCompatibilityLayer();
  validateTemplateManifest();

  console.log('JS boundary validation passed');
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
