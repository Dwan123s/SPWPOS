import {
  Children,
  Fragment,
  isValidElement,
  useSyncExternalStore,
  useEffect,
  useState,
} from "react";
import { getSnapshot, subscribe } from "./store.js";
import { state } from "./ui.jsx";
export function nodeList(nodes) {
  return nodes?.length ? Children.toArray(nodes) : null;
}
export function lines(nodes) {
  return nodes.map((node, i) => (
    <Fragment key={i}>
      {i > 0 && <br />}
      {node}
    </Fragment>
  ));
}
// Normalize trusted form configuration, never user supplied HTML.
export function attrs(config = "") {
  if (typeof config === "object") return config;
  const props = {},
    aliases = {
      maxlength: "maxLength",
      minlength: "minLength",
      autocomplete: "autoComplete",
      inputmode: "inputMode",
      readonly: "readOnly",
      checked: "defaultChecked",
      tabindex: "tabIndex",
    };
  const boolean = new Set([
    "required",
    "disabled",
    "checked",
    "selected",
    "open",
    "hidden",
    "multiple",
    "readonly",
  ]);
  for (const match of String(config).matchAll(
    /([\w-]+)(?:="([^"]*)"|='([^']*)'|=([^\s]+))?/g,
  )) {
    const name = match[1];
    props[aliases[name] || name] = boolean.has(name)
      ? true
      : (match[2] ?? match[3] ?? match[4] ?? "");
  }
  return props;
}
export function cssStyle(value) {
  if (typeof value === "object") return value;
  return Object.fromEntries(
    String(value)
      .split(";")
      .filter((p) => p.includes(":"))
      .map((p) => {
        const index = p.indexOf(":"),
          name = p.slice(0, index).trim();
        return [
          name.startsWith("--")
            ? name
            : name.replace(/-([a-z])/g, (_, c) => c.toUpperCase()),
          p.slice(index + 1).trim(),
        ];
      }),
  );
}
function flatten(children) {
  return Children.toArray(children).flatMap((child) =>
    isValidElement(child) && child.type === Fragment
      ? flatten(child.props.children)
      : [child],
  );
}
export function Select({ children, ...props }) {
  const options = flatten(children),
    selected = options.find((option) => option?.props?.selected);
  const value =
    props.defaultValue ?? selected?.props?.value ?? selected?.props?.children;
  return (
    <select {...props} defaultValue={value}>
      {options.map((option, i) => {
        if (!isValidElement(option)) return option;
        const { selected: ignored, ...rest } = option.props;
        return <option {...rest} key={option.key ?? i} />;
      })}
    </select>
  );
}
export function Slot({ as: Tag = "div", id, children, ...props }) {
  const snapshot = useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
  return (
    <Tag id={id} {...props}>
      {Object.hasOwn(snapshot.slots, id) ? snapshot.slots[id] : children}
    </Tag>
  );
}
export function SessionTimer({ id, className, compact = false }) {
  const [, tick] = useState(0);
  useEffect(() => {
    const timer = setInterval(() => tick((n) => n + 1), 1000);
    return () => clearInterval(timer);
  }, []);
  const s = state.workspace?.sessions.find((session) => session.id === id);
  const seconds = s
    ? Math.max(
        0,
        Math.floor(
          s.actualSeconds +
            (s.status === "Running" ? (Date.now() - state.loadedAt) / 1000 : 0),
        ),
      )
    : 0;
  const text = (
    compact && seconds < 3600
      ? [Math.floor(seconds / 60), seconds % 60]
      : [
          Math.floor(seconds / 3600),
          Math.floor(seconds / 60) % 60,
          seconds % 60,
        ]
  )
    .map((n) => String(n).padStart(2, "0"))
    .join(":");
  return (
    <span className={className} data-timer={id}>
      {text}
    </span>
  );
}
