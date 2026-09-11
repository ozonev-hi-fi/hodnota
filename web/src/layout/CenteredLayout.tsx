import type { ReactNode } from 'react';

function CenteredLayout({ children }: { children: ReactNode }) {
  return (
    <div className="container">
      <div className="row justify-content-center">
        <div className="col-12 col-md-8 col-lg-6 py-5">{children}</div>
      </div>
    </div>
  );
}

export default CenteredLayout;
