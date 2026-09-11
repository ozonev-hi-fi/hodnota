import { type KeyboardEvent, type ReactNode, useState } from 'react';

interface KeyboardNavigableListProps<T> {
  items: T[];
  getKey: (item: T) => string;
  renderItem: (item: T) => ReactNode;
  onActivate: (item: T) => void;
}

// Doesn't reset its own highlight when `items` changes — a caller whose item set can change
// (a new search) should pass a `key` prop that changes too, so React remounts this component
// fresh rather than it silently keeping (or losing) a highlight position that no longer makes
// sense for the new list.
//
// Plain divs, not ul/li: overriding a semantic list element's built-in role with a custom ARIA
// widget role (listbox/option) is discouraged — assistive tech's native handling of ul/li can
// conflict with the custom one layered on top. Focus stays on the listbox container itself
// (not on individual options) and aria-activedescendant tells assistive tech which option is
// current, per the WAI-ARIA listbox authoring pattern; each option also gets its own Enter
// handler so it's independently keyboard-operable, not just reachable via the container.
function KeyboardNavigableList<T>({
  items,
  getKey,
  renderItem,
  onActivate,
}: KeyboardNavigableListProps<T>) {
  const [highlightedIndex, setHighlightedIndex] = useState(0);

  function handleKeyDown(event: KeyboardEvent<HTMLDivElement>) {
    if (items.length === 0) {
      return;
    }

    if (event.key === 'ArrowDown') {
      event.preventDefault();
      setHighlightedIndex((index) => (index + 1) % items.length);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      setHighlightedIndex((index) => (index - 1 + items.length) % items.length);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      onActivate(items[highlightedIndex]);
    }
  }

  return (
    <div
      className="list-group"
      role="listbox"
      tabIndex={0}
      onKeyDown={handleKeyDown}
      aria-activedescendant={
        items.length > 0 ? getKey(items[highlightedIndex]) : undefined
      }
    >
      {items.map((item, index) => {
        const id = getKey(item);
        return (
          <div
            key={id}
            id={id}
            role="option"
            tabIndex={-1}
            aria-selected={index === highlightedIndex}
            className={`list-group-item list-group-item-action ${index === highlightedIndex ? 'active' : ''}`}
            onClick={() => onActivate(item)}
            onKeyDown={(event) => {
              if (event.key === 'Enter') {
                onActivate(item);
              }
            }}
            onMouseEnter={() => setHighlightedIndex(index)}
          >
            {renderItem(item)}
          </div>
        );
      })}
    </div>
  );
}

export default KeyboardNavigableList;
