interface ProviderLink {
  platform: string;
  url: string;
}

interface ProviderLinkGroupProps {
  category: string;
  links: ProviderLink[];
}

function ProviderLinkGroup({ category, links }: ProviderLinkGroupProps) {
  if (links.length === 0) {
    return null;
  }

  return (
    <div className="mb-4">
      <h2 className="h5">{category}</h2>
      <ul className="list-unstyled">
        {links.map((link) => (
          <li key={link.url} className="mb-2">
            <a href={link.url} target="_blank" rel="noreferrer">
              {link.platform}
            </a>
          </li>
        ))}
      </ul>
    </div>
  );
}

export default ProviderLinkGroup;
