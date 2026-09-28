import { Counter } from "./components/Counter";
import { FetchData } from "./components/FetchData";
import { Home } from "./components/Home";
import { Words } from "./components/Words";
import { Lists } from "./components/Lists";
import { ListPractice } from "./components/ListPractice";
import { Study } from "./components/Study";
import { BulkImport } from "./components/BulkImport";
import { KindleImport } from "./components/KindleImport";
import { LingQImport } from "./components/LingQImport";
import { NotesImport } from "./components/NotesImport";
import { ExportWords } from "./components/ExportWords";

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
    path: '/lists',
    element: <Lists />
  },
  {
    path: '/lists/practice',
    element: <ListPractice />
  },
  {
    path: '/study',
    element: <Study />
  },
  {
    path: '/bulk-import',
    element: <BulkImport />
  },
  {
    path: '/kindle-import',
    element: <KindleImport />
  },
  {
    path: '/lingq-import',
    element: <LingQImport />
  },
  {
    path: '/notes-import',
    element: <NotesImport />
  },
  {
    path: '/export-words',
    element: <ExportWords />
  }
];

export default AppRoutes;
