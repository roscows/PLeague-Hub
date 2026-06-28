import { Route, Routes } from 'react-router-dom';
import { Layout } from './components/Layout';
import { ProtectedRoute } from './routes/ProtectedRoute';
import { Forum } from './pages/Forum';
import { ForumDiscussionPage } from './pages/ForumDiscussion';
import { Home } from './pages/Home';
import { Login } from './pages/Login';
import { News } from './pages/News';
import { NewsDetailPage } from './pages/NewsDetail';
import { NewsSourcesPage } from './pages/NewsSources';
import { Register } from './pages/Register';
import { Results } from './pages/Results';
import { Stats } from './pages/Stats';
import { TablePage } from './pages/Table';
import { MatchDetailPage } from './pages/MatchDetail';
import { PlayerProfilePage } from './pages/PlayerProfile';
import { ClubProfilePage } from './pages/ClubProfile';

export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<Home />} />
        <Route path="results" element={<Results />} />
        <Route path="mec/:id" element={<MatchDetailPage />} />
        <Route path="stats" element={<Stats />} />
        <Route path="igrac/:id" element={<PlayerProfilePage />} />
        <Route path="klub/:id" element={<ClubProfilePage />} />
        <Route path="tabela" element={<TablePage />} />
        <Route path="news" element={<News />} />
        <Route path="news/:id" element={<NewsDetailPage />} />
        <Route path="forum" element={<Forum />} />
        <Route path="forum/:id" element={<ForumDiscussionPage />} />
        <Route path="login" element={<Login />} />
        <Route path="register" element={<Register />} />
        <Route element={<ProtectedRoute allowedRoles={['moderator', 'administrator']} />}>
          <Route path="news/sources" element={<NewsSourcesPage />} />
        </Route>
      </Route>
    </Routes>
  );
}
