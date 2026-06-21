import { Fragment } from 'react';
import type { CommentVoteValue, ForumCommentNode } from '../../types/api';
import { ForumComment } from './ForumComment';

interface ForumThreadProps {
  nodes: ForumCommentNode[];
  currentUserId?: string;
  replyingTo: string | null;
  onReply: (commentId: string) => void;
  onCancelReply: () => void;
  onSubmitReply: (commentId: string, text: string) => Promise<void>;
  onVote: (commentId: string, value: CommentVoteValue) => void;
}

export function ForumThread(props: ForumThreadProps) {
  function renderNodes(nodes: ForumCommentNode[]): React.ReactNode {
    return nodes.map((node) => (
      <Fragment key={node.id}>
        <ForumComment
          comment={node}
          currentUserId={props.currentUserId}
          isReplying={props.replyingTo === node.id}
          onCancelReply={props.onCancelReply}
          onReply={props.onReply}
          onSubmitReply={props.onSubmitReply}
          onVote={props.onVote}
        />
        {renderNodes(node.children)}
      </Fragment>
    ));
  }

  return <div className="space-y-2">{renderNodes(props.nodes)}</div>;
}
