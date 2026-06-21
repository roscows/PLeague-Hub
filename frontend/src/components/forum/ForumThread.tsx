import { Fragment, type ReactNode } from 'react';
import { canModerateRole } from '../../services/authorization';
import type { CommentVoteValue, ForumCommentNode, Role } from '../../types/api';
import { partitionPinnedThreads } from '../../utils/forumTree';
import { ForumComment } from './ForumComment';

interface ForumThreadProps {
  nodes: ForumCommentNode[];
  currentUserId?: string;
  currentUserRole?: Role;
  replyingTo: string | null;
  onReply: (commentId: string) => void;
  onCancelReply: () => void;
  onSubmitReply: (commentId: string, text: string) => Promise<void>;
  onVote: (commentId: string, value: CommentVoteValue) => void;
  onModerate: (comment: ForumCommentNode) => void;
  onTogglePin: (comment: ForumCommentNode) => void;
  onDelete: (comment: ForumCommentNode) => void;
}

export function ForumThread(props: ForumThreadProps) {
  const { pinned, regular } = partitionPinnedThreads(props.nodes);
  const numbers = new Map<string, number>();

  function collectNumbers(nodes: ForumCommentNode[]) {
    nodes.forEach((node) => {
      numbers.set(node.id, node.broj);
      collectNumbers(node.children);
    });
  }
  collectNumbers(props.nodes);

  function renderNodes(nodes: ForumCommentNode[]): ReactNode {
    return nodes.map((node) => (
      <Fragment key={node.id}>
        <ForumComment
          canModerate={canModerateRole(props.currentUserRole, node.autorUloga)}
          comment={node}
          currentUserId={props.currentUserId}
          isReplying={props.replyingTo === node.id}
          parentNumber={node.parentCommentId ? numbers.get(node.parentCommentId) : undefined}
          onCancelReply={props.onCancelReply}
          onDelete={() => props.onDelete(node)}
          onModerate={() => props.onModerate(node)}
          onReply={props.onReply}
          onSubmitReply={props.onSubmitReply}
          onTogglePin={() => props.onTogglePin(node)}
          onVote={props.onVote}
        />
        {renderNodes(node.children)}
      </Fragment>
    ));
  }

  return (
    <div className="min-w-0 max-w-full space-y-2 overflow-hidden">
      {pinned.length > 0 && (
        <>
          <h2 className="px-1 text-xs font-extrabold uppercase text-slate-500">Pinovani komentari</h2>
          {renderNodes(pinned)}
        </>
      )}
      {renderNodes(regular)}
    </div>
  );
}
