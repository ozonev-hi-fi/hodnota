import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import KeyboardNavigableList from './KeyboardNavigableList.tsx';

interface Item {
  id: string;
  label: string;
}

const items: Item[] = [
  { id: '1', label: 'Alpha' },
  { id: '2', label: 'Bravo' },
  { id: '3', label: 'Charlie' },
];

function renderList(onActivate: (item: Item) => void) {
  render(
    <KeyboardNavigableList
      items={items}
      getKey={(item) => item.id}
      renderItem={(item) => item.label}
      onActivate={onActivate}
    />,
  );
  return screen.getByRole('listbox');
}

describe('KeyboardNavigableList', () => {
  it('renders every item', () => {
    renderList(vi.fn());

    expect(screen.getByText('Alpha')).toBeInTheDocument();
    expect(screen.getByText('Bravo')).toBeInTheDocument();
    expect(screen.getByText('Charlie')).toBeInTheDocument();
  });

  it('highlights the first item by default', () => {
    renderList(vi.fn());

    expect(screen.getByRole('option', { name: 'Alpha' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
    expect(screen.getByRole('option', { name: 'Bravo' })).toHaveAttribute(
      'aria-selected',
      'false',
    );
  });

  it('moves the highlight down on ArrowDown', () => {
    const listbox = renderList(vi.fn());

    fireEvent.keyDown(listbox, { key: 'ArrowDown' });

    expect(screen.getByRole('option', { name: 'Bravo' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
  });

  it('wraps to the last item on ArrowUp from the first', () => {
    const listbox = renderList(vi.fn());

    fireEvent.keyDown(listbox, { key: 'ArrowUp' });

    expect(screen.getByRole('option', { name: 'Charlie' })).toHaveAttribute(
      'aria-selected',
      'true',
    );
  });

  it('activates the highlighted item on Enter', () => {
    const onActivate = vi.fn();
    const listbox = renderList(onActivate);

    fireEvent.keyDown(listbox, { key: 'ArrowDown' });
    fireEvent.keyDown(listbox, { key: 'Enter' });

    expect(onActivate).toHaveBeenCalledWith(items[1]);
  });

  it('activates an item directly on click, regardless of highlight', () => {
    const onActivate = vi.fn();
    renderList(onActivate);

    fireEvent.click(screen.getByText('Charlie'));

    expect(onActivate).toHaveBeenCalledWith(items[2]);
  });
});
