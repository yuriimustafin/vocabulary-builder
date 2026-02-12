import { Counter } from "./components/Counter";
import { FetchData } from "./components/FetchData";
import { Home } from "./components/Home";
import { Words } from "./components/Words";
import { BulkImport } from "./components/BulkImport";
import { KindleImport } from "./components/KindleImport";

const AppRoutes = [
  {
    index: true,
    element: <Home />
  },
  {
    path: '/counter',
    element: <Counter />
  },
  {
    path: '/fetch-data',
    element: <FetchData />
  },
  {
    path: '/words',
    element: <Words />
  },
  {
    path: '/bulk-import',
    element: <BulkImport />
  },
  {
    path: '/kindle-import',
    element: <KindleImport />
  }
];

export default AppRoutes;
