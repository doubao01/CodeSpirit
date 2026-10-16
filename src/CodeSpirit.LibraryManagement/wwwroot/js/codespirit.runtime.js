(function () {
  'use strict';

  installCompatibilityLayer();

  var NULL_VALUE = '';

  function installCompatibilityLayer() {
    if (!Array.from) {
      Array.from = function (value) {
        return Array.prototype.slice.call(value || []);
      };
    }

    if (!String.prototype.startsWith) {
      String.prototype.startsWith = function (search, position) {
        var start = position || 0;
        return this.substr(start, String(search).length) === String(search);
      };
    }

    if (!String.prototype.endsWith) {
      String.prototype.endsWith = function (search, length) {
        var source = String(this);
        var end = length == null ? source.length : length;
        return source.substring(end - String(search).length, end) === String(search);
      };
    }

    if (window.NodeList && !window.NodeList.prototype.forEach) {
      window.NodeList.prototype.forEach = Array.prototype.forEach;
    }

    var elementProto = window.Element && window.Element.prototype;
    if (elementProto && !elementProto.matches) {
      elementProto.matches = elementProto.msMatchesSelector || elementProto.webkitMatchesSelector;
    }

    if (elementProto && !elementProto.closest) {
      elementProto.closest = function (selector) {
        var el = this;
        while (el && el.nodeType === 1) {
          if (el.matches && el.matches(selector)) return el;
          el = el.parentElement || el.parentNode;
        }
        return null;
      };
    }

    if (typeof window.CustomEvent !== 'function') {
      window.CustomEvent = function (event, params) {
        var options = params || { bubbles: false, cancelable: false, detail: null };
        var customEvent = document.createEvent('CustomEvent');
        customEvent.initCustomEvent(event, Boolean(options.bubbles), Boolean(options.cancelable), options.detail);
        return customEvent;
      };
      window.CustomEvent.prototype = window.Event && window.Event.prototype;
    }
  }

  function selector(name, value) {
    var escaped = window.CSS && typeof window.CSS.escape === 'function'
      ? window.CSS.escape(String(value))
      : String(value).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
    return '[' + name + '="' + escaped + '"]';
  }

  function getViewModelRoot(element) {
    return element.closest('[data-cs-vm]');
  }

  function serializeForm(form) {
    var data = {};
    if (window.FormData && FormData.prototype && typeof FormData.prototype.forEach === 'function') {
      var formData = new FormData(form);
      formData.forEach(function (value, key) {
        appendFormValue(data, key, value);
      });
      return data;
    }

    Array.from(form.elements || []).forEach(function (el) {
      if (!el.name || el.disabled || el.type === 'file') return;
      if (el.type === 'checkbox' || el.type === 'radio') {
        if (!el.checked) return;
        appendFormValue(data, el.name, el.value || 'on');
        return;
      }
      if (el.tagName === 'SELECT' && el.multiple) {
        data[el.name] = Array.from(el.options).filter(function (option) { return option.selected; }).map(function (option) { return option.value; });
        return;
      }
      appendFormValue(data, el.name, el.value);
    });
    return data;
  }

  function appendFormValue(data, key, value) {
    if (Object.prototype.hasOwnProperty.call(data, key)) {
      if (!Array.isArray(data[key])) {
        data[key] = [data[key]];
      }
      data[key].push(value);
      return;
    }

    data[key] = value;
  }

  function hasFileInput(form) {
    return form.querySelector('input[type="file"]') !== null;
  }

  function postViewModel(form, payload, options) {
    var targetUrl = form.getAttribute('action') || (window.location && window.location.pathname) || '/';
    var method = form.getAttribute('method') || 'POST';
    var opts = options || {};
    var useFormData = hasFileInput(form);

    var fetchOptions = { method: method };
    if (useFormData) {
      var fd = new FormData();
      Object.keys(payload).forEach(function (key) {
        var el = form.querySelector(selector('name', key));
        if (el && el.type === 'file' && el.files && el.files.length) {
          fd.append(key, el.files[0]);
        } else {
          fd.append(key, payload[key] == null ? '' : payload[key]);
        }
      });
      fetchOptions.body = fd;
    } else {
      var headers = { 'Content-Type': 'application/json' };
      if (opts.headers) {
        Object.keys(opts.headers).forEach(function (k) { headers[k] = opts.headers[k]; });
      }
      fetchOptions.headers = headers;
      fetchOptions.body = JSON.stringify(payload);
    }

    if (typeof fetch !== 'function') {
      return Promise.reject(new Error('Fetch API is not available in this browser.'));
    }

    return fetch(targetUrl, fetchOptions).then(function (response) {
      if (!response.ok) {
        var status = response.status;
        return response.text().then(function (body) {
          var err = new Error('ViewModel request failed: ' + status);
          err.status = status;
          try { err.body = JSON.parse(body); } catch (_) { err.body = body; }
          throw err;
        });
      }
      if (response.status === 204) {
        return {};
      }
      return response.text().then(function (body) {
        if (!body || !body.trim()) return {};
        var contentType = response.headers && response.headers.get ? response.headers.get('content-type') || '' : '';
        if (contentType.indexOf('application/json') >= 0 || /^[\[{]/.test(body.trim())) {
          return JSON.parse(body);
        }
        return { body: body };
      });
    });
  }

  function readCommand(submitter) {
    if (!submitter) {
      return null;
    }

    return submitter.getAttribute('data-cs-command') || (submitter.name === '__command' ? submitter.value : null);
  }

  function updateField(root, name, value) {
    if (!root || !name) {
      return;
    }

    root.querySelectorAll(selector('name', name) + ', ' + selector('data-cs-bind', name)).forEach(function (element) {
      if ('value' in element || element.tagName === 'SELECT') {
        writeElementValue(element, value);
      } else {
        element.textContent = value == null ? NULL_VALUE : value;
      }
    });

    applyAttr(root, name, value);
    applyVisibility(root, name, value);
    applyCssClass(root, name, value);
    applyTone(root, name, value);
    applyEnabled(root, name, value);
  }

  function writeElementValue(element, value) {
    if (element.type === 'checkbox') {
      var checkboxValue = element.value || 'on';
      element.checked = Array.isArray(value) ? value.map(String).indexOf(String(checkboxValue)) >= 0 : value === true || String(value) === String(checkboxValue);
      return;
    }

    if (element.type === 'radio') {
      element.checked = String(value) === String(element.value);
      return;
    }

    if (element.tagName === 'SELECT' && element.multiple) {
      var values = Array.isArray(value) ? value.map(String) : [String(value == null ? NULL_VALUE : value)];
      Array.from(element.options || []).forEach(function (option) {
        option.selected = values.indexOf(String(option.value)) >= 0;
      });
      return;
    }

    element.value = value == null ? NULL_VALUE : value;
  }

  function queryAttr(root, attr, boundName) {
    var results = [];
    root.querySelectorAll('[' + attr + ']').forEach(function (el) {
      var expr = el.getAttribute(attr);
      var colonPos = expr.indexOf(':');
      var name = (colonPos >= 0 ? expr.substring(0, colonPos) : expr).trim();
      if (name === boundName) {
        results.push(el);
      }
    });
    return results;
  }

  function applyVisibility(root, name, value) {
    queryAttr(root, 'data-cs-visible', name).forEach(function (el) {
      var expr = el.getAttribute('data-cs-visible');
      var colonPos = expr.indexOf(':');
      var expected = colonPos >= 0 ? expr.substring(colonPos + 1).trim() : null;
      var visible = expected === null ? isTruthy(value) : String(value) === expected;
      el.style.display = visible ? '' : 'none';
    });

    queryAttr(root, 'data-cs-hidden', name).forEach(function (el) {
      var expr = el.getAttribute('data-cs-hidden');
      var colonPos = expr.indexOf(':');
      var expected = colonPos >= 0 ? expr.substring(colonPos + 1).trim() : null;
      var hidden = expected === null ? isTruthy(value) : String(value) === expected;
      el.style.display = hidden ? 'none' : '';
    });
  }

  function applyCssClass(root, name, value) {
    queryAttr(root, 'data-cs-class', name).forEach(function (el) {
      var expr = el.getAttribute('data-cs-class');
      var parts = expr.split(':');
      var className = parts.length > 1 ? parts[1].trim() : name.toLowerCase();
      var expected = parts.length > 2 ? parts[2].trim() : null;
      var shouldAdd = expected === null ? isTruthy(value) : String(value) === expected;
      if (shouldAdd) {
        el.classList.add(className);
      } else {
        el.classList.remove(className);
      }
    });
  }

  function applyTone(root, name, value) {
    queryAttr(root, 'data-cs-tone', name).forEach(function (el) {
      var expr = el.getAttribute('data-cs-tone');
      var parts = expr.split(':');
      var prefix;
      if (parts.length > 1 && parts[1].trim()) {
        prefix = parts[1].trim();
      } else {
        prefix = el.classList[0] || '';
      }
      if (!prefix) {
        return;
      }

      var prefixDash = prefix + '-';
      Array.from(el.classList).forEach(function (c) {
        if (c !== prefix && c.startsWith(prefixDash)) {
          el.classList.remove(c);
        }
      });

      var next = value;
      if (next == null || next === NULL_VALUE || String(next).trim() === '') {
        return;
      }
      el.classList.add(prefix + '-' + String(next));
    });
  }

  function applyAttr(root, name, value) {
    queryAttr(root, 'data-cs-attr', name).forEach(function (el) {
      var expr = el.getAttribute('data-cs-attr');
      var colonPos = expr.indexOf(':');
      var attrName = colonPos >= 0 ? expr.substring(colonPos + 1).trim() : name.toLowerCase();
      if (!isSafeDynamicAttribute(attrName, value)) {
        return;
      }
      if (value == null || value === NULL_VALUE) {
        el.removeAttribute(attrName);
      } else {
        el.setAttribute(attrName, String(value));
      }
    });
  }

  function isSafeDynamicAttribute(name, value) {
    if (!name || /^on/i.test(name) || String(name).toLowerCase() === 'srcdoc') {
      return false;
    }
    if (/^(href|src|action|formaction)$/i.test(name) && /^\s*javascript:/i.test(String(value || ''))) {
      return false;
    }
    return true;
  }

  function applyEnabled(root, name, value) {
    queryAttr(root, 'data-cs-enabled', name).forEach(function (el) {
      var enabled = isTruthy(value);
      el.disabled = !enabled;
    });

    queryAttr(root, 'data-cs-disabled', name).forEach(function (el) {
      var disabled = isTruthy(value);
      el.disabled = disabled;
    });
  }

  function isTruthy(value) {
    if (value == null || value === false || value === 0 || value === NULL_VALUE) {
      return false;
    }
    if (typeof value === 'string') {
      var lower = value.toLowerCase();
      if (lower === 'false' || lower === '0' || lower === 'no' || lower === 'off') {
        return false;
      }
    }
    return true;
  }

  function applyState(root, state) {
    Object.keys(state || {}).forEach(function (key) {
      updateField(root, key, state[key]);
    });
  }

  function applyRegions(root, regions) {
    Object.keys(regions || {}).forEach(function (name) {
      var current = document.querySelector(selector('data-cs-region', name));
      if (!current) {
        return;
      }

      var host = document.createElement('div');
      host.innerHTML = regions[name];
      var nodes = Array.from(host.children || []);
      if (!nodes.length) {
        return;
      }

      if (nodes.length === 1) {
        current.replaceWith(nodes[0]);
        mount(nodes[0]);
        return;
      }

      if (typeof current.replaceChildren === 'function') {
        current.replaceChildren.apply(current, nodes);
      } else {
        current.innerHTML = '';
        nodes.forEach(function (node) { current.appendChild(node); });
      }
      mount(current);
    });
  }

  function applyErrors(root, errors) {
    clearErrors(root);
    if (!errors) {
      return;
    }

    var summaryMessages = [];
    Object.keys(errors).forEach(function (fieldName) {
      var message = errors[fieldName];
      var matched = false;
      root.querySelectorAll(selector('name', fieldName) + ', ' + selector('data-cs-bind', fieldName)).forEach(function (el) {
        matched = true;
        el.classList.add('cs-invalid');
        var summary = el.closest('label') || el.parentElement;
        if (summary && !summary.querySelector('.cs-error')) {
          var span = document.createElement('span');
          span.className = 'cs-error';
          span.textContent = message;
          summary.appendChild(span);
        }
      });

      if (!matched) {
        summaryMessages.push(message);
      }
    });

    if (summaryMessages.length) {
      var summary = root.querySelector('.cs-error-summary');
      if (!summary) {
        summary = document.createElement('div');
        summary.className = 'cs-error-summary';
        summary.setAttribute('role', 'alert');
        root.appendChild(summary);
      }

      summary.innerHTML = '';
      summaryMessages.forEach(function (message) {
        var item = document.createElement('div');
        item.className = 'cs-error-summary-item';
        item.textContent = message;
        summary.appendChild(item);
      });
    }

    root.classList.add('cs-has-errors');
    emit(root, 'codespirit:validation', { errors: errors });
  }

  function clearErrors(root) {
    root.classList.remove('cs-has-errors');
    root.querySelectorAll('.cs-invalid').forEach(function (el) {
      el.classList.remove('cs-invalid');
    });
    root.querySelectorAll('.cs-error').forEach(function (el) {
      el.remove();
    });
    root.querySelectorAll('.cs-error-summary').forEach(function (el) {
      el.remove();
    });
  }

  function setLoading(form, isLoading, submitter) {
    var controls = form.querySelectorAll('button[type="submit"], input[type="submit"], button[data-cs-command]');
    if (isLoading) {
      form.classList.add('cs-loading');
      form.setAttribute('data-cs-busy', '');
      controls.forEach(function (btn) {
        if (btn.getAttribute('data-cs-prev-disabled') == null) {
          btn.setAttribute('data-cs-prev-disabled', btn.disabled ? 'true' : 'false');
        }
        btn.disabled = true;
      });
      if (submitter) {
        submitter.setAttribute('data-cs-submitting', '');
        if (submitter.getAttribute('data-cs-loading') === 'true' || form.getAttribute('data-cs-loading') === 'true') {
          submitter.classList.add('cs-btn-loading');
        }
      }
    } else {
      form.classList.remove('cs-loading');
      form.removeAttribute('data-cs-busy');
      controls.forEach(function (btn) {
        var prevDisabled = btn.getAttribute('data-cs-prev-disabled');
        if (prevDisabled !== null) {
          btn.disabled = prevDisabled === 'true';
          btn.removeAttribute('data-cs-prev-disabled');
        }
      });
      form.querySelectorAll('[data-cs-submitting]').forEach(function (btn) {
        btn.removeAttribute('data-cs-submitting');
        btn.classList.remove('cs-btn-loading');
      });
    }
  }

  function showRequestError(root, error) {
    var message = 'Request failed. Please try again.';
    if (error && error.body && typeof error.body === 'object' && error.body.message) {
      message = error.body.message;
    } else if (error && typeof error.body === 'string' && error.body.trim()) {
      message = error.body.trim();
    } else if (error && error.message) {
      message = error.message;
    }
    applyErrors(root, { __request: message });
  }

  function mount(root) {
    var target = root || document;
    if (window.CodeSpirit && window.CodeSpirit.ui && typeof window.CodeSpirit.ui.init === 'function') {
      window.CodeSpirit.ui.init(target);
    }
    return target;
  }

  function refresh(root) {
    return mount(root);
  }

  function emit(root, name, detail) {
    root.dispatchEvent(new CustomEvent(name, {
      bubbles: true,
      detail: detail
    }));
  }

  function handleSubmit(event) {
    var form = event.target;
    if (!form.matches('[data-cs-vm]')) {
      return;
    }

    var submitter = event.submitter || (lastSubmitter && lastSubmitter.form === form ? lastSubmitter : null);
    var debounceMs = parseInt(
      (submitter && submitter.getAttribute('data-cs-debounce')) ||
      form.getAttribute('data-cs-debounce'),
      10
    );
    if (!isNaN(debounceMs) && debounceMs > 0) {
      var now = Date.now();
      var lastSubmit = form._csLastSubmit || 0;
      if (now - lastSubmit < debounceMs) {
        event.preventDefault();
        return;
      }
      form._csLastSubmit = now;
    }

    event.preventDefault();
    var payload = serializeForm(form);
    setLoading(form, true, submitter);
    clearErrors(form);
    var command = readCommand(submitter);
    if (command) {
      payload.__command = command;
    } else if (submitter && submitter.name) {
      payload[submitter.name] = submitter.value;
    }

    postViewModel(form, payload).then(function (result) {
      setLoading(form, false);
      if (result.errors) {
        applyErrors(form, result.errors);
        emit(form, 'codespirit:error', result.errors);
        return;
      }
      applyRegions(form, result.regions);
      applyState(form, result.state || result);
      emit(form, 'codespirit:updated', result);
    }).catch(function (error) {
      setLoading(form, false);
      showRequestError(form, error);
      emit(form, 'codespirit:error', error);
    });
  }

  var lastSubmitter = null;

  function rememberSubmitter(event) {
    var target = event.target;
    var submitter = target && target.closest ? target.closest('button[type="submit"], input[type="submit"], button[data-cs-command]') : null;
    if (submitter && submitter.form) {
      lastSubmitter = submitter;
    }
  }

  function handleInput(event) {
    var detail = event.detail || {};
    var target = event.target;
    var root = getViewModelRoot(target);
    var name = detail.name || target.name || target.getAttribute('data-cs-bind');
    var value = detail.value != null ? detail.value : readInputEventValue(target);

    updateField(root, name, value);
    if (root && name) {
      emit(root, 'codespirit:changed', { name: name, value: value });
    }
  }

  function readInputEventValue(target) {
    if (target && target.type === 'checkbox') {
      return target.checked ? (target.value || 'on') : NULL_VALUE;
    }
    if (target && target.type === 'radio') {
      return target.checked ? target.value : NULL_VALUE;
    }
    return target ? target.value : NULL_VALUE;
  }

  function input(element, name, value) {
    if (!element) {
      return;
    }

    element.dispatchEvent(new CustomEvent('codespirit:input', {
      bubbles: true,
      detail: {
        name: name || element.name || element.getAttribute('data-cs-bind'),
        value: arguments.length > 2 ? value : element.value
      }
    }));
  }

  function qs(selector, root) {
    return (root || document).querySelector(selector);
  }

  function qsa(selector, root) {
    return Array.from((root || document).querySelectorAll(selector));
  }

  function on(root, event, selectorOrHandler, handler) {
    if (!root) root = document;
    var delegated = typeof selectorOrHandler === 'string';
    var callback = delegated ? handler : selectorOrHandler;
    if (typeof callback !== 'function') return function () {};

    var listener = delegated ? function (eventObject) {
      var match = eventObject.target && eventObject.target.closest ? eventObject.target.closest(selectorOrHandler) : null;
      if (match && root.contains(match)) {
        callback.call(match, eventObject, match);
      }
    } : callback;

    root.addEventListener(event, listener);
    return function () { root.removeEventListener(event, listener); };
  }

  function debounce(fn, delay) {
    var timer = null;
    return function () {
      var args = arguments;
      var self = this;
      clearTimeout(timer);
      timer = setTimeout(function () { fn.apply(self, args); }, delay || 0);
    };
  }

  function throttle(fn, delay) {
    var last = 0;
    var timer = null;
    return function () {
      var now = Date.now();
      var args = arguments;
      var self = this;
      var wait = (delay || 0) - (now - last);
      if (wait <= 0) {
        last = now;
        fn.apply(self, args);
        return;
      }
      clearTimeout(timer);
      timer = setTimeout(function () {
        last = Date.now();
        fn.apply(self, args);
      }, wait);
    };
  }

  function ready(callback) {
    if (document.readyState && document.readyState !== 'loading') {
      callback();
      return;
    }
    document.addEventListener('DOMContentLoaded', callback, { once: true });
  }

  function state(root) {
    return vm(root || document).state();
  }

  function set(root, name, value) {
    return arguments.length === 2 ? vm(document).set(root, name) : vm(root).set(name, value);
  }

  function batch(root, changes) {
    var chain = vm(root || document);
    chain.set(changes || {});
    refresh(chain._root);
    return chain;
  }

  var _observers = {};
  var _customValidators = {};

  function registerValidator(name, validatorFn) {
    if (typeof name === 'string' && typeof validatorFn === 'function') {
      _customValidators[name] = validatorFn;
    }
  }

  function notifyObservers(form, changes) {
    var id = form.getAttribute('data-cs-vm-id') || form.id || form.getAttribute('name') || '';
    var list = _observers[id];
    if (!list) {
      return;
    }

    list.forEach(function (entry) {
      Object.keys(changes).forEach(function (field) {
        if (entry.fields.indexOf(field) >= 0 || entry.fields.length === 0) {
          entry.callback(changes[field], field);
        }
      });
    });
  }

  function VmChain(root) {
    this._root = root;
    this._form = root.matches('[data-cs-vm]') ? root : (root.querySelector('[data-cs-vm]') || root.closest('[data-cs-vm]'));
    if (!this._form) {
      throw new Error('CodeSpirit.vm: no ViewModel form found in the element tree');
    }
    this._id = this._form.getAttribute('data-cs-vm-id') || this._form.id || this._form.getAttribute('name') || '';
    this._original = serializeForm(this._form);
    this._destroyed = false;
  }

  VmChain.prototype = {
    set: function (name, value) {
      this._checkDestroyed();
      if (arguments.length === 1 && typeof name === 'object') {
        var self = this;
        Object.keys(name).forEach(function (k) {
          self.set(k, name[k]);
        });
        return this;
      }
      updateField(this._form, name, value);
      var el = this._form.querySelector(selector('name', name));
      if (el) {
        writeElementValue(el, value);
        input(el, name, value);
      }
      var changes = {};
      changes[name] = value;
      notifyObservers(this._form, changes);
      return this;
    },

    get: function (name) {
      this._checkDestroyed();
      var fields = Array.from(this._form.querySelectorAll(selector('name', name)));
      if (!fields.length) {
        var bound = this._form.querySelector(selector('data-cs-bind', name));
        return bound ? bound.textContent : undefined;
      }

      if (fields[0].type === 'checkbox') {
        var checked = fields.filter(function (el) { return el.checked; }).map(function (el) { return el.value || 'on'; });
        return fields.length === 1 ? (checked[0] || NULL_VALUE) : checked;
      }

      if (fields[0].type === 'radio') {
        var selected = fields.filter(function (el) { return el.checked; })[0];
        return selected ? selected.value : NULL_VALUE;
      }

      if (fields[0].tagName === 'SELECT' && fields[0].multiple) {
        return Array.from(fields[0].options || []).filter(function (option) { return option.selected; }).map(function (option) { return option.value; });
      }

      return fields[0].value;
    },

    val: function (name, value) {
      this._checkDestroyed();
      return arguments.length < 2 ? this.get(name) : this.set(name, value);
    },

    state: function () {
      this._checkDestroyed();
      return serializeForm(this._form);
    },

    reset: function (names) {
      this._checkDestroyed();
      var self = this;
      if (names) {
        var changes = {};
        names.forEach(function (n) {
          var orig = Object.prototype.hasOwnProperty.call(self._original, n) ? self._original[n] : NULL_VALUE;
          self.set(n, orig);
          changes[n] = orig;
        });
        notifyObservers(this._form, changes);
      } else {
        Object.keys(this._original).forEach(function (k) {
          self.set(k, self._original[k]);
        });
        notifyObservers(this._form, this._original);
      }
      clearErrors(this._form);
      emit(this._form, 'codespirit:reset', {});
      return this;
    },

    invoke: function (command, options) {
      this._checkDestroyed();
      var form = this._form;
      setLoading(form, true);
      clearErrors(form);
      var payload = serializeForm(form);
      if (command) {
        payload.__command = command;
      }

      return postViewModel(form, payload, options).then(function (result) {
        setLoading(form, false);
        if (result.errors) {
          applyErrors(form, result.errors);
          emit(form, 'codespirit:error', result.errors);
          return result;
        }
        applyRegions(form, result.regions);
        applyState(form, result.state || result);
        emit(form, 'codespirit:updated', result);
        return result;
      }).catch(function (error) {
        setLoading(form, false);
        showRequestError(form, error);
        emit(form, 'codespirit:error', error);
        throw error;
      });
    },

    submit: function () {
      this._checkDestroyed();
      if (typeof this._form.requestSubmit === 'function') {
        this._form.requestSubmit();
      } else {
        this._form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
      }
      return this;
    },

    validate: function (rules) {
      this._checkDestroyed();
      clearErrors(this._form);
      if (!rules) {
        return true;
      }

      var self = this;
      var valid = true;
      var errors = {};
      Object.keys(rules).forEach(function (field) {
        var fieldRules = rules[field];
        var value = self.get(field);
        if (value === undefined) {
          value = null;
        }

        if (fieldRules.required && (value == null || String(value).trim() === NULL_VALUE)) {
          errors[field] = fieldRules.message || (field + ' is required');
          valid = false;
          return;
        }

        if (fieldRules.minLength != null && String(value || '').length < fieldRules.minLength) {
          errors[field] = fieldRules.message || (field + ' must be at least ' + fieldRules.minLength + ' characters');
          valid = false;
          return;
        }

        if (fieldRules.maxLength != null && String(value || '').length > fieldRules.maxLength) {
          errors[field] = fieldRules.message || (field + ' must be at most ' + fieldRules.maxLength + ' characters');
          valid = false;
          return;
        }

        if (fieldRules.pattern) {
          var pattern = typeof fieldRules.pattern === 'string' ? new RegExp(fieldRules.pattern) : fieldRules.pattern;
          if (!pattern.test(String(value || ''))) {
            errors[field] = fieldRules.message || (field + ' is invalid');
            valid = false;
            return;
          }
        }

        if (fieldRules.email && !/^\S+@\S+\.\S+$/.test(String(value || ''))) {
          errors[field] = fieldRules.message || (field + ' is invalid');
          valid = false;
          return;
        }

        if (fieldRules.url && !/^https?:\/\/\S+$/i.test(String(value || '').trim())) {
          errors[field] = fieldRules.message || (field + ' must be a valid URL');
          valid = false;
          return;
        }

        if (fieldRules.numeric && !/^-?\d+(\.\d+)?$/.test(String(value || '').trim())) {
          errors[field] = fieldRules.message || (field + ' must be numeric');
          valid = false;
          return;
        }

        if (fieldRules.integer && !/^-?\d+$/.test(String(value || '').trim())) {
          errors[field] = fieldRules.message || (field + ' must be an integer');
          valid = false;
          return;
        }

        if ((fieldRules.phone || fieldRules.tel) && !/^[+]?[\d\s-]{7,20}$/.test(String(value || '').trim())) {
          errors[field] = fieldRules.message || (field + ' must be a valid phone number');
          valid = false;
          return;
        }

        if (fieldRules.min != null && parseFloat(value) < fieldRules.min) {
          errors[field] = fieldRules.message || (field + ' must be at least ' + fieldRules.min);
          valid = false;
          return;
        }

        if (fieldRules.max != null && parseFloat(value) > fieldRules.max) {
          errors[field] = fieldRules.message || (field + ' must be at most ' + fieldRules.max);
          valid = false;
          return;
        }

        if (fieldRules.matches) {
          var targetVal = self.get(fieldRules.matches);
          if (value !== targetVal) {
            errors[field] = fieldRules.message || (field + ' does not match ' + fieldRules.matches);
            valid = false;
            return;
          }
        }

        for (var customRule in _customValidators) {
          if (Object.prototype.hasOwnProperty.call(_customValidators, customRule) && fieldRules[customRule]) {
            var ruleResult = _customValidators[customRule](value, fieldRules[customRule], field, self);
            if (ruleResult !== true) {
              errors[field] = fieldRules.message || (typeof ruleResult === 'string' ? ruleResult : (field + ' is invalid'));
              valid = false;
              return;
            }
          }
        }

        if (fieldRules.custom && typeof fieldRules.custom === 'function') {
          var customResult = fieldRules.custom(value, field);
          if (customResult !== true) {
            errors[field] = typeof customResult === 'string' ? customResult : (field + ' is invalid');
            valid = false;
            return;
          }
        }
      });

      if (!valid) {
        applyErrors(this._form, errors);
      }

      return valid;
    },

    observe: function (fields, callback) {
      this._checkDestroyed();
      if (!this._id) {
        this._id = 'vm_' + Date.now();
        this._form.setAttribute('data-cs-vm-id', this._id);
      }
      _observers[this._id] = _observers[this._id] || [];
      _observers[this._id].push({
        fields: Array.isArray(fields) ? fields : (fields ? [fields] : []),
        callback: callback
      });
      return this;
    },

    on: function (event, handler) {
      this._checkDestroyed();
      this._root.addEventListener(event, handler);
      return this;
    },

    off: function (event, handler) {
      this._checkDestroyed();
      this._root.removeEventListener(event, handler);
      return this;
    },

    once: function (event, handler) {
      this._checkDestroyed();
      var self = this;
      var wrapped = function (e) {
        self.off(event, wrapped);
        handler.call(this, e);
      };
      return this.on(event, wrapped);
    },

    destroy: function () {
      if (this._destroyed) {
        return;
      }
      this._destroyed = true;
      if (this._id && _observers[this._id]) {
        delete _observers[this._id];
      }
    },

    refresh: function () {
      this._checkDestroyed();
      refresh(this._root);
      return this;
    },

    el: function (query) {
      this._checkDestroyed();
      return this._form.querySelector(query);
    },

    all: function (query) {
      this._checkDestroyed();
      return Array.from(this._form.querySelectorAll(query));
    },

    _checkDestroyed: function () {
      if (this._destroyed) {
        throw new Error('CodeSpirit.vm: chain has been destroyed');
      }
    }
  };

  function vm(root) {
    if (typeof root === 'string') {
      root = document.querySelector(root);
    }
    if (!root) {
      throw new Error('CodeSpirit.vm: element not found');
    }
    return new VmChain(root);
  }

  document.addEventListener('click', rememberSubmitter, true);
  document.addEventListener('submit', handleSubmit);
  document.addEventListener('input', handleInput);
  document.addEventListener('codespirit:input', handleInput);

  window.CodeSpirit = window.CodeSpirit || {};
  window.CodeSpirit.applyState = applyState;
  window.CodeSpirit.applyRegions = applyRegions;
  window.CodeSpirit.applyErrors = applyErrors;
  window.CodeSpirit.clearErrors = clearErrors;
  window.CodeSpirit.input = input;
  window.CodeSpirit.qs = qs;
  window.CodeSpirit.qsa = qsa;
  window.CodeSpirit.on = on;
  window.CodeSpirit.debounce = debounce;
  window.CodeSpirit.throttle = throttle;
  window.CodeSpirit.ready = ready;
  window.CodeSpirit.state = state;
  window.CodeSpirit.set = set;
  window.CodeSpirit.batch = batch;
  window.CodeSpirit.mount = mount;
  window.CodeSpirit.refresh = refresh;
  window.CodeSpirit.updateField = updateField;
  window.CodeSpirit.vm = vm;
  window.CodeSpirit.VmChain = VmChain;
  window.CodeSpirit.registerValidator = registerValidator;
  window.CodeSpirit.validators = _customValidators;
  if (!window.$cs) {
    window.$cs = window.CodeSpirit;
  }
})();
