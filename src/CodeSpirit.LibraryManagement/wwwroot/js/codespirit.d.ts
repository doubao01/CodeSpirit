/**
 * CodeSpirit MVVM Runtime — TypeScript type declarations
 * 
 * Provides full IntelliSense for all CodeSpirit client-side APIs.
 * Reference this file with a /// <reference path="..." /> comment
 * at the top of your page scripts, or add it to your tsconfig.json.
 *
 * @license MIT
 */

// ===================================================================
// Event detail types
// ===================================================================

interface CodespiritFieldEventDetail {
  name: string;
  value: unknown;
}

interface CodespiritViewModelResult<TServerState = Record<string, unknown>> {
  state?: TServerState;
  regions?: Record<string, string>;
  errors?: Record<string, string>;
}

interface CodespiritErrorEventDetail {
  [field: string]: string;
}

interface CodespiritModalEventDetail {
  modal: HTMLElement;
}

interface CodespiritTreeToggleEventDetail {
  value: string | null;
  expanded: boolean;
}

interface CodespiritWizardStepEventDetail {
  step: string;
}

// ===================================================================
// VmChain — fluent ViewModel manipulation API
// ===================================================================

/**
 * Validation rule for a single form field.
 */
interface CodespiritFieldRules {
  /** Field is required */
  required?: boolean;
  /** Minimum string length */
  minLength?: number;
  /** Maximum string length */
  maxLength?: number;
  /** Regex pattern to test against */
  pattern?: RegExp | string;
  /** Email address format validation */
  email?: boolean;
  /** URL format validation */
  url?: boolean;
  /** Numeric value validation (integer or decimal) */
  numeric?: boolean;
  /** Integer value validation */
  integer?: boolean;
  /** Phone number validation */
  phone?: boolean;
  /** Phone number validation alias */
  tel?: boolean;
  /** Cross-field matching rule, comparing value with another field */
  matches?: string;
  /** Minimum numeric value */
  min?: number;
  /** Maximum numeric value */
  max?: number;
  /** Custom validation function. Return true for valid, or a string error message */
  custom?: (value: unknown, field: string) => boolean | string;
  /** Custom error message overriding defaults */
  message?: string;
  /** Arbitrary custom validator rules */
  [customRule: string]: unknown;
}

/**
 * Observer callback — invoked when observed field values change.
 * @param value - The new field value
 * @param field - The field name that changed
 */
type CodespiritObserverCallback = (value: unknown, field: string) => void;

/**
 * Event handler for VmChain event subscriptions.
 */
type CodespiritEventHandler<T = unknown> = (event: CustomEvent<T>) => void;

/**
 * Custom event types emitted by CodeSpirit.
 */
type CodespiritEventType =
  | 'codespirit:updated'
  | 'codespirit:changed'
  | 'codespirit:error'
  | 'codespirit:validation'
  | 'codespirit:reset'
  | 'codespirit:input'
  | 'codespirit:modal-closed'
  | 'codespirit:tree-toggle'
  | 'codespirit:wizard-step';

interface CodespiritEventDetailMap {
  'codespirit:updated': CodespiritViewModelResult;
  'codespirit:changed': CodespiritFieldEventDetail;
  'codespirit:error': CodespiritErrorEventDetail;
  'codespirit:validation': { errors: CodespiritErrorEventDetail };
  'codespirit:reset': Record<string, never>;
  'codespirit:input': CodespiritFieldEventDetail;
  'codespirit:modal-closed': CodespiritModalEventDetail;
  'codespirit:tree-toggle': CodespiritTreeToggleEventDetail;
  'codespirit:wizard-step': CodespiritWizardStepEventDetail;
}

/**
 * Options for postViewModel fetch requests.
 */
interface CodespiritPostOptions {
  headers?: Record<string, string>;
}

/**
 * Fluent ViewModel chain object returned by {@link CodeSpiritNamespace.vm | CodeSpirit.vm()}.
 * All methods return `this` for chaining except getters/terminal operations.
 */
interface VmChain {
  /**
   * Set field value(s) on the ViewModel form.
   * @param name - Field name, or an object of { field: value } pairs for batch set
   * @param value - Field value (ignored if name is an object)
   * @returns this for chaining
   *
   * @example
   * // Single field
   * CodeSpirit.vm(root).set('Search', 'bob');
   * // Batch set
   * CodeSpirit.vm(root).set({ Search: 'bob', Category: 'fiction' });
   */
  set(name: string, value: unknown): this;
  set(state: Record<string, unknown>): this;

  /**
   * Get the current value of a field.
   * @param name - Field name
   * @returns The field value, or undefined if the field doesn't exist
   */
  get(name: string): string | string[] | undefined;

  /**
   * Get or set a field value. With one argument acts as getter,
   * with two arguments acts as setter.
   * @param name - Field name
   * @param value - Field value (optional — if provided, sets the value)
   * @returns Field value (getter) or this (setter)
   */
  val(name: string): string | string[] | undefined;
  val(name: string, value: unknown): this;

  /**
   * Captures the current state of all form fields.
   * @returns An object mapping field names to their current values
   */
  state(): Record<string, unknown>;

  /**
   * Resets form fields to their original values.
   * @param names - Optional array of field names to reset; resets all fields if omitted
   * @returns this for chaining
   */
  reset(names?: string[]): this;

  /**
   * Invokes a server-side [Command] method.
   * Sends current form state to the server and applies any returned
   * state/regions/errors updates.
   * 
   * @param command - The command name (must match a [Command] method on the ViewModel)
   * @param options - Optional fetch options (custom headers etc.)
   * @returns Promise resolving to the server response
   *
   * @example
   * vm.invoke('Refresh').then(function (result) {
   *   console.log('Updated state:', result.state);
   * });
   */
  invoke(command?: string, options?: CodespiritPostOptions): Promise<CodespiritViewModelResult>;

  /**
   * Programmatically trigger the form's submit event.
   * Equivalent to calling `form.requestSubmit()`.
   * @returns this for chaining
   */
  submit(): this;

  /**
   * Client-side form validation with declarative rules.
   * Displays error messages on invalid fields via CodeSpirit's error rendering.
   * 
   * @param rules - Object mapping field names to validation rules
   * @returns true if all validations pass, false otherwise
   *
   * @example
   * var valid = CodeSpirit.vm(root).validate({
   *   Name: { required: true, minLength: 2, message: 'Name is required' },
   *   Email: { required: true, email: true, message: 'Invalid email' },
   *   Age: { min: 18, max: 65 }
   * });
   */
  validate(rules: Record<string, CodespiritFieldRules>): boolean;

  /**
   * Observe changes to one or more fields. The callback fires whenever
   * any observed field value changes (via VmChain.set() or CodeSpirit.input()).
   * 
   * @param fields - A single field name, an array of field names, or empty array/string to observe all
   * @param callback - Invoked with (value, fieldName) on each change
   * @returns this for chaining
   *
   * @example
   * CodeSpirit.vm(root).observe('Search', function (val, field) {
   *   console.log(field + ' changed to', val);
   * });
   */
  observe(fields: string | string[], callback: CodespiritObserverCallback): this;

  /**
   * Subscribe to a CodeSpirit event on the ViewModel's root element.
   * @param event - Event name (e.g. 'codespirit:updated', 'codespirit:changed')
   * @param handler - Event handler function
   * @returns this for chaining
   *
   * @example
   * CodeSpirit.vm(root).on('codespirit:updated', function (e) {
   *   console.log('Updated:', e.detail);
   * });
   */
  on<TEvent extends CodespiritEventType>(event: TEvent, handler: CodespiritEventHandler<CodespiritEventDetailMap[TEvent]>): this;
  on(event: string, handler: CodespiritEventHandler): this;

  /**
   * Unsubscribe a previously registered event handler.
   * @param event - Event name
   * @param handler - The original handler function reference
   * @returns this for chaining
   */
  off<TEvent extends CodespiritEventType>(event: TEvent, handler: CodespiritEventHandler<CodespiritEventDetailMap[TEvent]>): this;
  off(event: string, handler: CodespiritEventHandler): this;

  /**
   * Subscribe to an event that fires only once, then auto-unsubscribes.
   * @param event - Event name
   * @param handler - Event handler function
   * @returns this for chaining
   */
  once<TEvent extends CodespiritEventType>(event: TEvent, handler: CodespiritEventHandler<CodespiritEventDetailMap[TEvent]>): this;
  once(event: string, handler: CodespiritEventHandler): this;

  /**
   * Clean up the chain, removing any registered observers.
   * After destruction, all methods will throw an error.
   */
  destroy(): void;

  /**
   * Re-initialize UI behaviors on the ViewModel's root after dynamic DOM updates.
   * @returns this for chaining
   */
  refresh(): this;

  /**
   * Query a single element within the ViewModel form scope.
   * @param query - CSS selector
   * @returns The first matching element, or null
   */
  el(query: string): Element | null;

  /**
   * Query all matching elements within the ViewModel form scope.
   * @param query - CSS selector
   * @returns Array of matching elements
   */
  all(query: string): Element[];
}

// ===================================================================
// UI Behaviors API
// ===================================================================

/**
 * UI behavior initializer — called with all pending elements for a behavior name.
 * @param elements - Array of DOM elements with data-ui matching this name
 * @param root - The root element being initialized
 */
type CodespiritUiInitializer = (elements: HTMLElement[], root?: HTMLElement | Document) => void;

type CodespiritBuiltInUiBehavior =
  | 'datepicker'
  | 'clickable-card'
  | 'confirm-click'
  | 'auto-submit'
  | 'tabs'
  | 'modal'
  | 'wizard'
  | 'tree';

/**
 * UI behavior system API.
 */
interface CodespiritUI {
  /**
   * Register a reusable `data-ui` behavior.
   * Behavior names must match /^[A-Za-z0-9_-]+$/.
   * 
   * @param name - Behavior name (used as data-ui="name")
   * @param initializer - Function called with pending elements on mount/refresh
   * @throws If name contains invalid characters
   *
   * @example
   * CodeSpirit.ui.register('tooltip', function (elements) {
   *   elements.forEach(function (el) {
   *     el.addEventListener('mouseenter', showTooltip);
   *   });
   * });
   */
  register(name: CodespiritBuiltInUiBehavior | string, initializer: CodespiritUiInitializer): void;

  /**
   * Initialize all pending UI behaviors on a DOM root.
   * Called automatically on DOMContentLoaded and after state updates.
   * @param root - The root element to scan (defaults to document)
   */
  init(root?: HTMLElement | Document): void;

  /**
   * Mark elements as already initialized for a given behavior name.
   * This prevents the behavior from re-running on the same elements.
   * @param elements - Array of elements to mark
   * @param name - Behavior name
   */
  ready(elements: HTMLElement[], name: CodespiritBuiltInUiBehavior | string): void;
}

// ===================================================================
// Theme & Scene API
// ===================================================================

/**
 * A map of CSS custom property names to their computed values.
 * Keys are --cs-* CSS variable names.
 */
type CodespiritThemeTokens = Record<string, string>;

/**
 * Theme and scene inspection API.
 */
interface CodespiritTheme {
  /**
   * Export the current scene's CSS custom property values.
   * Resolves --cs-primary, --cs-accent, --cs-bg, etc. from computed styles.
   * Falls back to framework defaults for any unresolvable values.
   * 
   * @param root - The scene root element (defaults to document)
   * @returns Map of CSS variable names to values
   */
  tokens(root?: HTMLElement | Document): CodespiritThemeTokens;

  /**
   * Shorthand alias for {@link CodespiritTheme.tokens}.
   */
  exportTokens(root?: HTMLElement | Document): CodespiritThemeTokens;
}

// ===================================================================
// Intent Analyzer API
// ===================================================================

type CodespiritIntentAnalyzer = (elements: HTMLElement[], root?: HTMLElement | Document) => void;

interface CodespiritIntent {
  /**
   * Run all registered intent analyzers on a DOM root.
   * Scans for elements with data-cs-intent attributes and applies
   * semantic tone classes (intent-success, intent-danger, etc.)
   * as well as scene detection (cs-scene-* classes).
   * 
   * Called automatically on DOMContentLoaded and after state updates.
   * @param root - The root element to analyze (defaults to document)
   */
  analyze(root?: HTMLElement | Document): void;

  /**
   * Register a custom intent analyzer.
   * Analyzer names must match /^[A-Za-z0-9_-]+$/.
   * 
   * Built-in analyzers: numeric, status, due, trend
   * 
   * @param name - Analyzer name (used as data-cs-intent="name")
   * @param initializer - Function called with matching elements
   * @throws If name contains invalid characters
   */
  register(name: string, initializer: CodespiritIntentAnalyzer): void;
}

/**
 * Tiny expression engine for declarative UI attributes.
 * Used by data-cs-show and data-cs-enable.
 *
 * Supported operators: > < >= <= == != && || ! contains empty ? :
 */
interface CodespiritExpression {
  /**
   * Evaluate an expression against a ViewModel form state.
   *
   * @example
   * CodeSpirit.expression.evaluate("BookCount > 0 && City contains 'Paris'", form);
   */
  evaluate(expression: string, form: HTMLElement): unknown;

  /** Parse an expression into an internal AST object. */
  parse(expression: string): unknown;

  /** Return true when a string contains supported expression operators. */
  isExpr(value: string): boolean;

  /** Apply data-cs-show and data-cs-enable expressions under a ViewModel root. */
  apply(root: HTMLElement): void;
}

// ===================================================================
// Main CodeSpirit namespace (global)
// ===================================================================

/**
 * The global CodeSpirit client runtime namespace.
 * All methods and sub-namespaces are available via `window.CodeSpirit`
 * or the shorthand `window.$cs`.
 */
interface CodeSpiritNamespace {
  // -- Static utility methods --

  /**
   * Apply server-returned state to bound elements.
   * Updates all elements with matching [name] or [data-cs-bind] attributes,
   * plus conditional visibility, CSS class, tone, attr, and enabled/disabled bindings.
   * 
   * @param root - The ViewModel root element (form or container with data-cs-vm)
   * @param state - Object of { fieldName: value } pairs
   *
   * @example
   * CodeSpirit.applyState(form, { Search: 'hello', Count: 42 });
   */
  applyState(root: HTMLElement, state: Record<string, unknown>): void;

  /**
   * Replace region placeholders with server-rendered HTML fragments.
   * Finds elements with [data-cs-region="name"] and replaces their content
   * with the corresponding HTML string from the regions object.
   * 
   * @param root - The ViewModel root element
   * @param regions - Object of { regionName: htmlString } pairs
   */
  applyRegions(root: HTMLElement, regions: Record<string, string>): void;

  /**
   * Display validation errors on form fields and show an error summary.
   * Adds cs-invalid class to matched fields and appends cs-error span with the message.
   * Unmatched error keys are collected into a cs-error-summary container.
   * 
   * @param root - The ViewModel root element
   * @param errors - Object of { fieldName: errorMessage } pairs
   */
  applyErrors(root: HTMLElement, errors: Record<string, string> | null | undefined): void;

  /**
   * Remove all validation error indicators from the form.
   * Clears cs-invalid classes, cs-error spans, and cs-error-summary container.
   * 
   * @param root - The ViewModel root element
   */
  clearErrors(root: HTMLElement): void;

  /**
   * Notify the MVVM runtime that a control's value has changed.
   * Dispatches a codespirit:input event which triggers field updates,
   * conditional bindings, and observer callbacks.
   * 
   * @param element - The form control element (input, select, textarea)
   * @param name - Optional explicit field name (defaults to element.name or data-cs-bind)
   * @param value - Optional explicit value (defaults to element.value)
   *
   * @example
   * // Basic usage: notify that an input changed
   * CodeSpirit.input(document.querySelector('[name="Search"]'));
   * // Explicit name/value:
   * CodeSpirit.input(el, 'Search', 'bob');
   */
  input(element: HTMLElement, name?: string, value?: unknown): void;

  /** Query one element with optional scope. */
  qs<T extends Element = Element>(selector: string, root?: ParentNode): T | null;

  /** Query all elements with optional scope and return a real array. */
  qsa<T extends Element = Element>(selector: string, root?: ParentNode): T[];

  /** Bind a direct or delegated event and return an unsubscribe function. */
  on(root: Element | Document, event: string, handler: EventListener): () => void;
  on(root: Element | Document, event: string, selector: string, handler: (event: Event, match: Element) => void): () => void;

  /** Create a debounced function for search and live preview inputs. */
  debounce<T extends (...args: any[]) => void>(fn: T, delay?: number): T;

  /** Create a throttled function for scroll and resize handlers. */
  throttle<T extends (...args: any[]) => void>(fn: T, delay?: number): T;

  /** Run callback when DOM is ready. */
  ready(callback: () => void): void;

  /** Read current ViewModel form state. */
  state(root?: string | HTMLElement): Record<string, unknown>;

  /** Set one field using the nearest ViewModel form. */
  set(name: string, value: unknown): VmChain;
  set(root: string | HTMLElement, name: string, value: unknown): VmChain;

  /** Batch-update fields and refresh UI behaviors. */
  batch(root: string | HTMLElement, changes: Record<string, unknown>): VmChain;

  /**
   * First-time initialization of the MVVM runtime on a DOM root.
   * Triggers UI behavior initialization for all data-ui elements.
   * Call this after inserting new HTML into the page.
   * 
   * @param root - The root element to initialize (defaults to document)
   * @returns The initialized root element
   */
  mount(root?: HTMLElement | Document): HTMLElement | Document;

  /**
   * Re-initialize the MVVM runtime after dynamic DOM updates.
   * Alias for mount(). Call after replacing HTML fragments to re-attach
   * behaviors, intent analysis, and event listeners.
   * 
   * @param root - The root element to refresh
   * @returns The refreshed root element
   */
  refresh(root?: HTMLElement | Document): HTMLElement | Document;

  /**
   * Update a single bound field across all its manifestations.
   * Updates element values/text, and all conditional bindings
   * (visibility, CSS class, tone, attr, enabled/disabled).
   * 
   * @param root - The ViewModel root element
   * @param name - The field name to update
   * @param value - The new value
   */
  updateField(root: HTMLElement, name: string, value: unknown): void;

  /**
   * Create a fluent ViewModel chain for a DOM root.
   * The root must contain a form with [data-cs-vm] attribute,
   * or be one itself.
   * 
   * @param root - A CSS selector string or DOM element
   * @returns A VmChain instance for fluent manipulation
   * @throws If no ViewModel form is found in the element tree
   *
   * @example
   * var vm = CodeSpirit.vm('#myForm');
   * vm.set('Search', 'bob').invoke('Search').then(function (r) {
   *   console.log(r.state);
   * });
   */
  vm(root: string | HTMLElement): VmChain;

  /**
   * VmChain constructor (exposed for instanceof checks).
   * Prefer using CodeSpirit.vm() for creating instances.
   */
  VmChain: { new(root: HTMLElement): VmChain };

  /**
   * Register a custom validator rule globally.
   *
   * @param name - The validator rule name
   * @param validator - The validation callback
   */
  registerValidator(name: string, validator: (value: unknown, ruleParam: unknown, field: string, chain: VmChain) => boolean | string): void;

  /** Registry of custom validator functions */
  validators: Record<string, Function>;

  // -- Sub-namespaces --

  /** UI behavior system */
  ui: CodespiritUI;

  /** Intent analysis and scene detection */
  intent: CodespiritIntent;

  /** Theme token export */
  theme: CodespiritTheme;

  /** Declarative expression engine for data-cs-show and data-cs-enable */
  expression: CodespiritExpression;
}

// ===================================================================
// Global declarations
// ===================================================================

declare global {
  interface Window {
    /** CodeSpirit client runtime — primary API entry point */
    CodeSpirit: CodeSpiritNamespace;
    /** Shorthand alias for window.CodeSpirit */
    $cs: CodeSpiritNamespace;
  }

  interface HTMLElementEventMap {
    'codespirit:updated': CustomEvent<CodespiritViewModelResult>;
    'codespirit:changed': CustomEvent<CodespiritFieldEventDetail>;
    'codespirit:error': CustomEvent<CodespiritErrorEventDetail>;
    'codespirit:validation': CustomEvent<{ errors: CodespiritErrorEventDetail }>;
    'codespirit:reset': CustomEvent<Record<string, never>>;
    'codespirit:input': CustomEvent<CodespiritFieldEventDetail>;
    'codespirit:modal-closed': CustomEvent<CodespiritModalEventDetail>;
    'codespirit:tree-toggle': CustomEvent<CodespiritTreeToggleEventDetail>;
    'codespirit:wizard-step': CustomEvent<CodespiritWizardStepEventDetail>;
  }
}

export {};
