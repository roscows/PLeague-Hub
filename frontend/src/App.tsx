import { Route, Routes } from 'react-router-dom';
import { Layout } from './components/Layout';
import { ProtectedRoute } from './routes/ProtectedRoute';
import { Forum } from './pages/Forum';
import { ForumDiscussionPage } from './pages/ForumDiscussion';
import { Home } from './pages/Home';
import { Login } from './pages/Login';
import { News } from './pages/News';
import { NewsDetailPage } from './pages/NewsDetail';
import { Profile } from './pages/Profile';
import { Results } from './pages/Results';
import { Stats } from './pages/Stats';

export function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<Home />} />
        <Route path="results" element={<Results />} />
        <Route path="stats" element={<Stats />} />
        <Route path="news" element={<News />} />
        <Route path="news/:id" element={<NewsDetailPage />} />
        <Route path="forum" element={<Forum />} />
        <Route path="forum/:id" element={<ForumDiscussionPage />} />
        <Route path="login" element={<Login />} />
        <Route element={<ProtectedRoute />}>
          <Route path="profile" element={<Profile />} />
        </Route>
      </Route>
    </Routes>
  );
}
