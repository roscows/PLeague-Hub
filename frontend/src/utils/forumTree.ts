import type { ForumComment, ForumCommentNode } from '../types/api';

function compareComments(left: ForumCommentNode, right: ForumCommentNode) {
  const dateDifference = new Date(left.datumKreiranja).getTime() - new Date(right.datumKreiranja).getTime();
  return dateDifference || left.broj - right.broj;
}

export function buildCommentTree(comments: ForumComment[]): ForumCommentNode[] {
  const nodes = new Map<string, ForumCommentNode>();

  comments.forEach((comment) => {
    nodes.set(comment.id, { ...comment, children: [], depth: 1 });
  });

  const roots: ForumCommentNode[] = [];

  nodes.forEach((node) => {
    const parent = node.parentCommentId ? nodes.get(node.parentCommentId) : undefined;

    if (parent && parent.id !== node.id) {
      parent.children.push(node);
    } else {
      roots.push(node);
    }
  });

  function assignDepth(node: ForumCommentNode, parentDepth: number) {
    node.depth = Math.min(parentDepth + 1, 3);
    node.children.sort(compareComments);
    node.children.forEach((child) => assignDepth(child, node.depth));
  }

  roots.sort(compareComments);
  roots.forEach((root) => {
    root.depth = 1;
    root.children.sort(compareComments);
    root.children.forEach((child) => assignDepth(child, 1));
  });

  return roots;
}
